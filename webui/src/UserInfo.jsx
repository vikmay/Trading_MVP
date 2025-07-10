import React from 'react';
import { useAuth } from './useAuth';

const UserInfo = () => {
    const { user, logout, isAuthenticated } = useAuth();

    if (!isAuthenticated || !user) {
        return null;
    }

    return (
        <div className="user-info">
            <div className="user-details">
                <span className="user-name">
                    {user.firstName} {user.lastName}
                </span>
                <span className="user-email">{user.email}</span>
            </div>
            <button onClick={logout} className="logout-btn">
                Logout
            </button>
        </div>
    );
};

export default UserInfo;
