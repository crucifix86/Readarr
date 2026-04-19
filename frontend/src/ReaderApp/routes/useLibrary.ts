import { useCallback, useEffect, useState } from 'react';
import { apiFetch } from '../api';
import { ReaderUser } from '../auth';
import { BookSummary } from '../components/BookCard';

// Matches UserMeController.ReaderLibraryItem server-side.
interface RawLibraryItem {
  bookId: number;
  title: string;
  authorName: string;
  firstBookFileId: number | null;
  firstBookFileFormat: string | null;
  isFavorite: boolean;
  progressPercent: number | null;
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

    apiFetch<RawLibraryItem[]>(user, '/user/me/library')
      .then((items) => {
        if (cancelled) return;
        const summaries: BookSummary[] = (items || []).map((i) => ({
          id: i.bookId,
          title: i.title,
          authorName: i.authorName,
          firstBookFileId: i.firstBookFileId,
          firstBookFileFormat: i.firstBookFileFormat,
          isFavorite: i.isFavorite,
          progressPercent: i.progressPercent,
        }));
        setBooks(summaries);
        setLoading(false);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : String(err));
        setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [user, version]);

  return { books, loading, error, reload, applyFavoriteChange };
}
