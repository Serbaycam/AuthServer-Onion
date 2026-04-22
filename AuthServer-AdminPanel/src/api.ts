const API_BASE_URL = 'http://localhost:8080/api';

async function refreshTokens() {
  const storedTokens = localStorage.getItem('auth_tokens');
  if (!storedTokens) return null;
  
  const tokens = JSON.parse(storedTokens);
  try {
    const res = await fetch(`${API_BASE_URL}/Auth/refresh-token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken
      })
    });
    
    if (!res.ok) return null;
    
    const data = await res.json();
    if (data.succeeded && data.data) {
      localStorage.setItem('auth_tokens', JSON.stringify(data.data));
      return data.data.accessToken;
    }
    return null;
  } catch (error) {
    return null;
  }
}

export async function fetchWithAuth(endpoint: string, options: RequestInit = {}) {
  let storedTokens = localStorage.getItem('auth_tokens');
  let accessToken = storedTokens ? JSON.parse(storedTokens).accessToken : null;

  const headers = new Headers(options.headers || {});
  headers.set('Content-Type', 'application/json');
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  const config: RequestInit = {
    ...options,
    headers
  };

  let response = await fetch(`${API_BASE_URL}${endpoint}`, config);

  if (response.status === 401 && storedTokens) {
    // Try to refresh token
    const newAccessToken = await refreshTokens();
    if (newAccessToken) {
      headers.set('Authorization', `Bearer ${newAccessToken}`);
      response = await fetch(`${API_BASE_URL}${endpoint}`, { ...options, headers });
    } else {
      // Force logout if refresh failed
      localStorage.removeItem('auth_tokens');
      window.location.href = '/login';
    }
  }

  return response;
}
