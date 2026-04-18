import React from 'react';
import { useAuth } from '../auth';
import BookCard, { BookSummary, toggleFavorite } from '../components/BookCard';
import { useLibrary } from './useLibrary';

function Library() {
  const { user } = useAuth();
  const { books, loading, error, applyFavoriteChange } = useLibrary(user);

  const onToggleFav = async (book: BookSummary) => {
    const next = await toggleFavorite(user, book);
    applyFavoriteChange(book.id, next);
  };

  if (loading) {
    return <div className="pageStatus">Loading library…</div>;
  }

  if (error) {
    return <div className="pageStatus pageError">{error}</div>;
  }

  if (books.length === 0) {
    return (
      <div className="pageStatus">
        No books yet. Ask your admin to add some.
      </div>
    );
  }

  return (
    <div className="page">
      <h2 className="pageTitle">Library</h2>
      <div className="bookGrid">
        {books.map((b) => (
          <BookCard key={b.id} book={b} onToggleFavorite={onToggleFav} />
        ))}
      </div>
    </div>
  );
}

export default Library;
