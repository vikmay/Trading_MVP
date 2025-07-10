import React, { createContext, useState, useEffect } from 'react';
import { authApi, tokenStorage } from './authUtils';

export const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        // Check for existing token on mount
        const storedToken = tokenStorage.getToken();
        const storedUser = tokenStorage.getUser();

        if (storedToken && storedUser) {
            setToken(storedToken);
            setUser(storedUser);
        }
        setLoading(false);
    }, []);

    const login = async (email, password) => {
        try {
            const data = await authApi.login(email, password);

            setToken(data.token);
            setUser(data.user);

            tokenStorage.setToken(data.token);
            tokenStorage.setUser(data.user);

            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    };

    const register = async (email, password, firstName, lastName) => {
        try {
            const data = await authApi.register(
                email,
                password,
                firstName,
                lastName
            );

            setToken(data.token);
            setUser(data.user);

            tokenStorage.setToken(data.token);
            tokenStorage.setUser(data.user);

            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    };

    const logout = () => {
        setToken(null);
        setUser(null);
        tokenStorage.removeToken();
        tokenStorage.removeUser();
    };

    const refreshToken = async () => {
        try {
            const data = await authApi.refreshToken(token);
            setToken(data.token);
            tokenStorage.setToken(data.token);
            return data.token;
        } catch {
            logout();
            return null;
        }
    };

    const value = {
        user,
        token,
        login,
        register,
        logout,
        refreshToken,
        isAuthenticated: !!token,
        loading,
    };

    return (
        <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
    );
};
