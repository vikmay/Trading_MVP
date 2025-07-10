using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Auth.Models;
using Auth.Services;
using System.Security.Claims;

namespace Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid email or password"
                });
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid email or password"
                });
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var token = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            _logger.LogInformation("User {Email} logged in successfully", request.Email);

            return Ok(new AuthResponse
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                User = new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role
                },
                Message = "Login successful"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", request.Email);
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred during login"
            });
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "User with this email already exists"
                });
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = "Trader" // Default role
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            var token = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            _logger.LogInformation("User {Email} registered successfully", request.Email);

            return Ok(new AuthResponse
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                User = new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role
                },
                Message = "Registration successful"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", request.Email);
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred during registration"
            });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized();
            }

            return Ok(new UserInfo
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, "An error occurred");
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var principal = _tokenService.GetPrincipalFromExpiredToken(request.Token);
            var userId = principal.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid token"
                });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var newToken = _tokenService.GenerateAccessToken(user);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            return Ok(new AuthResponse
            {
                Success = true,
                Token = newToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                User = new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role
                },
                Message = "Token refreshed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Invalid token"
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "Auth", Timestamp = DateTime.UtcNow });
    }

    [HttpGet("google-login")]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrl = $"{Request.Scheme}://{Request.Host}/auth/google-callback";
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };
        properties.SetParameter("prompt", "select_account");
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("/auth/google-callback")]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Google authentication failed"
                });
            }

            var claims = result.Principal?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var firstName = claims?.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
            var lastName = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value;
            var googleId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Unable to get email from Google account"
                });
            }

            // Check if user exists
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Create new user
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName ?? "",
                    LastName = lastName ?? "",
                    Role = "User",
                    GoogleId = googleId,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Failed to create user account"
                    });
                }

                // Add user to default role
                await _userManager.AddToRoleAsync(user, "User");
                _logger.LogInformation("New user created via Google OAuth: {Email}", email);
            }
            else
            {
                // Update existing user with Google ID if not set
                if (string.IsNullOrEmpty(user.GoogleId))
                {
                    user.GoogleId = googleId;
                    await _userManager.UpdateAsync(user);
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("Existing user logged in via Google OAuth: {Email}", email);
            }

            // Generate JWT token
            var token = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            var authResponse = new AuthResponse
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                User = new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role
                },
                Message = "Google authentication successful"
            };

            // If this is an API call, return JSON
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Ok(authResponse);
            }

            // For web browser, redirect with token
            var redirectUri = !string.IsNullOrEmpty(returnUrl) ? returnUrl : "http://localhost:5173";
            var tokenParam = $"?token={token}&refreshToken={refreshToken}";
            return Redirect($"{redirectUri}{tokenParam}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google OAuth callback");
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Google authentication failed"
            });
        }
    }

    [HttpPost("google-token")]
    public async Task<ActionResult<AuthResponse>> GoogleTokenLogin([FromBody] GoogleTokenRequest request)
    {
        try
        {
            // This endpoint is for mobile/SPA apps that get the Google token directly
            // You would validate the Google token here and extract user info
            // For now, this is a placeholder - you'd need to add Google token validation

            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Google token authentication not implemented yet"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google token authentication");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred during Google authentication"
            });
        }
    }
}
