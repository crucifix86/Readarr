import React from 'react';
import { useAuth } from '../auth';
import BookCard, { BookSummary, toggleFavorite } from '../components/BookCard';
import { useLibrary } from './useLibrary';

function CurrentlyReading() {
  const { user } = useAuth();
  const { books, loading, error, applyFavoriteChange } = useLibrary(user);

  const inProgress = books
    .filter(
      (b) =>
        b.progressPercent != null &&
        b.progressPercent > 0 &&
        b.progressPercent < 0.99
    )
    .sort((a, b) => (b.progressPercent ?? 0) - (a.progressPercent ?? 0));

  const onToggleFav = async (book: BookSummary) => {
    const next = await toggleFavorite(user, book);
    applyFavoriteChange(book.id, next);
  };

  if (loading) {
    return <div className="pageStatus">Loading…</div>;
  }

  if (error) {
    return <div className="pageStatus pageError">{error}</div>;
  }

  if (inProgress.length === 0) {
    return (
      <div className="pageStatus">
        Nothing in progress. Open a book from the Library and start reading.
      </div>
    );
  }

  return (
    <div className="page">
      <h2 className="pageTitle">Currently Reading</h2>
      <div className="bookGrid">
        {inProgress.map((b) => (
          <BookCard key={b.id} book={b} onToggleFavorite={onToggleFav} />
        ))}
      </div>
    </div>
  );
}

export default CurrentlyReading;
