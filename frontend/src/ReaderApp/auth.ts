import { createContext, useContext } from 'react';

export interface ReaderUser {
  id: number;
  username: string;
  role: string;
  apiKey: string;
}

const STORAGE_KEY = 'readarr-reader-user';

export function loadStoredUser(): ReaderUser | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as ReaderUser) : null;
  } catch {
    return null;
  }
}

export function storeUser(user: ReaderUser | null): void {
  if (user) {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
  } else {
    window.localStorage.removeItem(STORAGE_KEY);
  }
}

export interface AuthState {
  user: ReaderUser | null;
  setUser: (user: ReaderUser | null) => void;
}

export const AuthContext = createContext<AuthState>({
  user: null,
  setUser: () => {
    /* overridden by provider */
  },
});

export function useAuth(): AuthState {
  return useContext(AuthContext);
}
