import React, { createContext, useContext, useState, useEffect } from 'react';

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

interface User {
  email: string;
  roles: string[];
}

interface AuthContextType {
  isAuthenticated: boolean;
  tokens: AuthTokens | null;
  user: User | null;
  login: (tokens: AuthTokens, tokenRawPayload?: any) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [tokens, setTokens] = useState<AuthTokens | null>(null);
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Check local storage on initial load
    const storedTokens = localStorage.getItem('auth_tokens');
    if (storedTokens) {
      try {
        const parsed = JSON.parse(storedTokens);
        setTokens(parsed);
        // Simple decode for visual purposes, actual validation is backend
        const payload = JSON.parse(atob(parsed.accessToken.split('.')[1]));
        setUser({
          email: payload.email || '',
          roles: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] 
                 ? (Array.isArray(payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']) 
                    ? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] 
                    : [payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']])
                 : []
        });
      } catch (e) {
        localStorage.removeItem('auth_tokens');
      }
    }
    setLoading(false);
  }, []);

  const login = (newTokens: AuthTokens, tokenRawPayload?: any) => {
    setTokens(newTokens);
    localStorage.setItem('auth_tokens', JSON.stringify(newTokens));
    if (newTokens.accessToken) {
        const payload = JSON.parse(atob(newTokens.accessToken.split('.')[1]));
        setUser({
            email: payload.email || '',
            roles: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] 
                 ? (Array.isArray(payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']) 
                    ? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] 
                    : [payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']])
                 : []
        });
    }
  };

  const logout = () => {
    setTokens(null);
    setUser(null);
    localStorage.removeItem('auth_tokens');
  };

  if (loading) return <div style={{ color: 'white' }}>Loading...</div>;

  return (
    <AuthContext.Provider value={{ isAuthenticated: !!tokens, tokens, user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
