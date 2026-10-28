import { create } from 'zustand';
import { extractClaims, isTokenExpired, UserProfile } from './tokenUtils';

export type { UserProfile } from './tokenUtils';

export interface AuthState {
  token: string | null;
  user: UserProfile | null;
  isAuthenticated: boolean;
  login: (token: string) => void;
  logout: () => void;
}

function getStoredToken(): string | null {
  try {
    const raw = localStorage.getItem('tokens');
    if (!raw) return null;
    let tokenStr: string | null = null;
    try {
      const parsed = JSON.parse(raw);
      tokenStr = parsed?.accessToken || parsed?.token || (typeof parsed === 'string' ? parsed : null);
    } catch {
      tokenStr = raw;
    }

    if (tokenStr && isTokenExpired(tokenStr)) {
      localStorage.removeItem('tokens');
      return null;
    }

    return tokenStr;
  } catch {
    return null;
  }
}

// Synchronous initial state from localStorage
const initialToken = getStoredToken();
const initialUser = initialToken ? extractClaims(initialToken) : null;
const initialIsAuthenticated = Boolean(initialToken);

export const useAuthStore = create<AuthState>((set) => ({
  token: initialToken,
  user: initialUser,
  isAuthenticated: initialIsAuthenticated,

  login: (token: string) => {
    let actualToken = token;
    try {
      const parsed = JSON.parse(token);
      if (parsed?.accessToken) actualToken = parsed.accessToken;
      else if (parsed?.token) actualToken = parsed.token;
    } catch {
      // already plain token string
    }

    localStorage.setItem('tokens', JSON.stringify({ accessToken: actualToken }));
    const user = extractClaims(actualToken);
    set({
      token: actualToken,
      user,
      isAuthenticated: true,
    });
  },

  logout: () => {
    localStorage.removeItem('tokens');
    set({
      token: null,
      user: null,
      isAuthenticated: false,
    });
  },
}));
