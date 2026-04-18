import React from 'react';
import { useAuth } from '../auth';
import BookCard, { BookSummary, toggleFavorite } from '../components/BookCard';
import { useLibrary } from './useLibrary';

function Favorites() {
  const { user } = useAuth();
  const { books, loading, error, applyFavoriteChange } = useLibrary(user);

  const favorites = books.filter((b) => b.isFavorite);

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

  if (favorites.length === 0) {
    return (
      <div className="pageStatus">
        No favorites yet. Tap the ☆ on any book to save it here.
      </div>
    );
  }

  return (
    <div className="page">
      <h2 className="pageTitle">Favorites</h2>
      <div className="bookGrid">
        {favorites.map((b) => (
          <BookCard key={b.id} book={b} onToggleFavorite={onToggleFav} />
        ))}
      </div>
    </div>
  );
}

export default Favorites;
