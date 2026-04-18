import React from 'react';
import { Link } from 'react-router-dom';
import { apiFetch, coverUrl } from '../api';
import { useAuth } from '../auth';

export interface BookSummary {
  id: number;
  title: string;
  authorName: string;
  firstBookFileId: number | null;
  firstBookFileFormat: string | null;
  isFavorite: boolean;
  progressPercent: number | null;
}

interface BookCardProps {
  book: BookSummary;
  onToggleFavorite: (book: BookSummary) => void;
}

function BookCard(props: BookCardProps) {
  const { user } = useAuth();
  const { book } = props;
  const readable =
    book.firstBookFileId != null &&
    (book.firstBookFileFormat === 'epub' || book.firstBookFileFormat === 'pdf');

  const onFavClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    props.onToggleFavorite(book);
  };

  const cover = user ? coverUrl(user, book.id) : '';

  const card = (
    <>
      <div className="bookCardCover">
        {cover && <img src={cover} alt="" loading="lazy" />}
        {book.progressPercent != null && book.progressPercent > 0 && (
          <div className="bookCardProgress">
            <div
              className="bookCardProgressBar"
              style={{
                width: `${Math.min(
                  100,
                  Math.round(book.progressPercent * 100)
                )}%`,
              }}
            />
          </div>
        )}
        <button
          type="button"
          className={`bookCardFav${book.isFavorite ? ' on' : ''}`}
          onClick={onFavClick}
          aria-label={book.isFavorite ? 'Remove favorite' : 'Add favorite'}
        >
          {book.isFavorite ? '★' : '☆'}
        </button>
      </div>
      <div className="book-card-meta">
        <div className="bookCardTitle" title={book.title}>
          {book.title}
        </div>
        <div className="bookCardAuthor" title={book.authorName}>
          {book.authorName}
        </div>
      </div>
    </>
  );

  if (readable) {
    return (
      <Link to={`/read/${book.firstBookFileId}`} className="bookCard">
        {card}
      </Link>
    );
  }

  return <div className="bookCard bookCardDisabled">{card}</div>;
}

export default BookCard;

// Hoisted so routes can call it without duplicating logic.
export async function toggleFavorite(
  user: ReturnType<typeof useAuth>['user'],
  book: BookSummary
): Promise<boolean> {
  if (!user) {
    return book.isFavorite;
  }

  if (book.isFavorite) {
    await apiFetch(user, `/user/me/favorites/${book.id}`, { method: 'DELETE' });
    return false;
  }

  await apiFetch(user, `/user/me/favorites/${book.id}`, { method: 'POST' });
  return true;
}
