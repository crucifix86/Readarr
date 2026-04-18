import { ReaderUser, storeUser } from './auth';

declare global {
  interface Window {
    ReadarrReader: { urlBase: string };
  }
}

export function urlBase(): string {
  return (window.ReadarrReader && window.ReadarrReader.urlBase) || '';
}

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export async function login(
  username: string,
  password: string
): Promise<ReaderUser> {
  const res = await fetch(`${urlBase()}/reader/api/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  });

  if (!res.ok) {
    const body = await res.json().catch(() => ({ message: res.statusText }));
    throw new ApiError(res.status, body.message || 'Login failed');
  }

  return (await res.json()) as ReaderUser;
}

export async function apiFetch<T = unknown>(
  user: ReaderUser | null,
  path: string,
  init: RequestInit = {}
): Promise<T> {
  if (!user) {
    throw new ApiError(401, 'Not logged in');
  }

  const headers = new Headers(init.headers || {});
  headers.set('X-Api-Key', user.apiKey);

  if (init.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const res = await fetch(`${urlBase()}/api/v1${path}`, { ...init, headers });

  if (res.status === 401) {
    storeUser(null);
    throw new ApiError(401, 'Session expired');
  }

  if (res.status === 204) {
    return null as unknown as T;
  }

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new ApiError(res.status, text);
  }

  const contentType = res.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    return (await res.json()) as T;
  }

  return null as unknown as T;
}

export function coverUrl(user: ReaderUser | null, bookId: number): string {
  if (!user) {
    return '';
  }

  return `${urlBase()}/MediaCover/Books/${bookId}/cover.jpg?apikey=${encodeURIComponent(
    user.apiKey
  )}`;
}

export function contentUrl(
  user: ReaderUser | null,
  bookFileId: number
): string {
  if (!user) {
    return '';
  }

  return `${urlBase()}/api/v1/bookfile/${bookFileId}/content?apikey=${encodeURIComponent(
    user.apiKey
  )}`;
}
