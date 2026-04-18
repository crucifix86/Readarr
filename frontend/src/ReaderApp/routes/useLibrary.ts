import { useCallback, useEffect, useState } from 'react';
import { apiFetch } from '../api';
import { ReaderUser } from '../auth';
import { BookSummary } from '../components/BookCard';

interface RawBookFile {
  id: number;
  path: string;
}

interface RawBook {
  id: number;
  title: string;
  authorTitle?: string;
  author?: { authorName?: string };
  statistics?: { bookFileCount?: number };
}

interface RawProgress {
  bookFileId: number;
  percent: number | null;
}

interface RawFavorite {
  bookId: number;
}

function extFor(path: string): string | null {
  const dot = path.lastIndexOf('.');
  return dot >= 0 ? path.slice(dot + 1).toLowerCase() : null;
}

export interface LibraryState {
  books: BookSummary[];
  loading: boolean;
  error: string | null;
  reload: () => void;
  applyFavoriteChange: (bookId: number, isFavorite: boolean) => void;
}

export function useLibrary(user: ReaderUser | null): LibraryState {
  const [books, setBooks] = useState<BookSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [version, setVersion] = useState(0);

  const reload = useCallback(() => setVersion((v) => v + 1), []);

  const applyFavoriteChange = useCallback(
    (bookId: number, isFavorite: boolean) => {
      setBooks((prev) =>
        prev.map((b) => (b.id === bookId ? { ...b, isFavorite } : b))
      );
    },
    []
  );

  useEffect(() => {
    if (!user) {
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    (async () => {
      try {
        const [rawBooks, files, favorites, progress] = await Promise.all([
          apiFetch<RawBook[]>(user, '/book'),
          apiFetch<RawBookFile[]>(user, '/bookfile'),
          apiFetch<RawFavorite[]>(user, '/user/me/favorites').catch(
            () => [] as RawFavorite[]
          ),
          apiFetch<RawProgress[]>(user, '/user/me/progress').catch(
            () => [] as RawProgress[]
          ),
        ]);

        const fileByBookId = new Map<number, RawBookFile>();
        for (const f of files || []) {
          // BookFile resource includes `bookId` too — probe both shapes.
          const bookId = (f as unknown as { bookId?: number }).bookId;
          if (bookId && !fileByBookId.has(bookId)) {
            fileByBookId.set(bookId, f);
          }
        }

        const favSet = new Set<number>((favorites || []).map((f) => f.bookId));
        const progressByFile = new Map<number, number>();
        for (const p of progress || []) {
          if (p.percent != null) {
            progressByFile.set(p.bookFileId, p.percent);
          }
        }

        const summaries: BookSummary[] = (rawBooks || [])
          .filter((b) => (b.statistics?.bookFileCount ?? 0) > 0)
          .map((b) => {
            const file = fileByBookId.get(b.id);
            const ext = file ? extFor(file.path) : null;

            return {
              id: b.id,
              title: b.title,
              authorName: b.author?.authorName || b.authorTitle || 'Unknown',
              firstBookFileId: file ? file.id : null,
              firstBookFileFormat: ext,
              isFavorite: favSet.has(b.id),
              progressPercent: file
                ? progressByFile.get(file.id) ?? null
                : null,
            };
          })
          .sort((a, b) => a.title.localeCompare(b.title));

        if (!cancelled) {
          setBooks(summaries);
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : String(err));
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [user, version]);

  return { books, loading, error, reload, applyFavoriteChange };
}
