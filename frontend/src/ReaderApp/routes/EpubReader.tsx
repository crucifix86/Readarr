import ePub, { Book, NavItem, Rendition } from 'epubjs';
import React, { useEffect, useRef, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { apiFetch } from '../api';
import { ReaderUser } from '../auth';

interface Bookmark {
  id: number;
  bookFileId: number;
  location: string;
  note: string | null;
  createdAt: string;
}

interface Props {
  user: ReaderUser;
  bookFileId: number;
  contentUrl: string;
  initialLocation: string | null;
}

// Swipe threshold — anything less than this is treated as a tap/scroll, not a page flip.
const SWIPE_MIN_PX = 40;

function EpubReader(props: Props) {
  const history = useHistory();
  const viewerRef = useRef<HTMLDivElement>(null);
  const bookRef = useRef<Book | null>(null);
  const renditionRef = useRef<Rendition | null>(null);
  const saveTimerRef = useRef<number | null>(null);
  const touchStartX = useRef<number | null>(null);

  const [toc, setToc] = useState<NavItem[]>([]);
  const [bookmarks, setBookmarks] = useState<Bookmark[]>([]);
  const [showSidebar, setShowSidebar] = useState(false);

  useEffect(() => {
    const book = ePub(props.contentUrl, { openAs: 'epub' });
    bookRef.current = book;

    const rendition = book.renderTo(viewerRef.current as HTMLElement, {
      width: '100%',
      height: '100%',
      flow: 'paginated',
      manager: 'default',
    });
    renditionRef.current = rendition;

    (props.initialLocation
      ? rendition.display(props.initialLocation)
      : rendition.display()
    ).catch(() => {
      rendition.display();
    });

    book.loaded.navigation.then((nav) => setToc(nav.toc || []));

    rendition.on(
      'relocated',
      (location: { start?: { cfi: string; percentage?: number } }) => {
        if (!location?.start) {
          return;
        }

        const cfi = location.start.cfi;
        const percent =
          typeof location.start.percentage === 'number'
            ? location.start.percentage
            : null;

        if (saveTimerRef.current) {
          window.clearTimeout(saveTimerRef.current);
        }
        saveTimerRef.current = window.setTimeout(() => {
          apiFetch(props.user, `/user/me/progress/${props.bookFileId}`, {
            method: 'PUT',
            body: JSON.stringify({ location: cfi, percent }),
          }).catch(() => {
            /* noop */
          });
        }, 1200);
      }
    );

    // eslint-disable-next-line no-use-before-define
    loadBookmarks();

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'ArrowRight' || e.key === 'PageDown') {
        rendition.next();
      }
      if (e.key === 'ArrowLeft' || e.key === 'PageUp') {
        rendition.prev();
      }
      if (e.key === 'Escape') {
        history.goBack();
      }
    };
    document.addEventListener('keydown', onKeyDown);

    return () => {
      document.removeEventListener('keydown', onKeyDown);
      if (saveTimerRef.current) {
        window.clearTimeout(saveTimerRef.current);
      }
      rendition.destroy();
      book.destroy();
    };
    // Re-mount if bookFileId or contentUrl changes (which only happens on route change)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.bookFileId, props.contentUrl]);

  function loadBookmarks() {
    apiFetch<Bookmark[]>(
      props.user,
      `/user/me/bookmarks?bookFileId=${props.bookFileId}`
    )
      .then((data) => setBookmarks(data || []))
      .catch(() => setBookmarks([]));
  }

  const onPrev = () => renditionRef.current?.prev();
  const onNext = () => renditionRef.current?.next();

  const onTouchStart = (e: React.TouchEvent) => {
    touchStartX.current = e.touches[0].clientX;
  };

  const onTouchEnd = (e: React.TouchEvent) => {
    if (touchStartX.current == null) {
      return;
    }
    const dx = e.changedTouches[0].clientX - touchStartX.current;
    touchStartX.current = null;
    if (Math.abs(dx) < SWIPE_MIN_PX) {
      return;
    }
    if (dx < 0) {
      onNext();
    } else {
      onPrev();
    }
  };

  const onAddBookmark = async () => {
    const loc = renditionRef.current?.currentLocation() as
      | { start?: { cfi: string } }
      | undefined;
    const cfi = loc?.start?.cfi;
    if (!cfi) {
      return;
    }
    await apiFetch(props.user, '/user/me/bookmarks', {
      method: 'POST',
      body: JSON.stringify({ bookFileId: props.bookFileId, location: cfi }),
    });
    loadBookmarks();
  };

  const onDeleteBookmark = async (id: number) => {
    await apiFetch(props.user, `/user/me/bookmarks/${id}`, {
      method: 'DELETE',
    });
    loadBookmarks();
  };

  const onChapterSelect = (href: string) => {
    renditionRef.current?.display(href);
    setShowSidebar(false);
  };

  const onGotoBookmark = (cfi: string) => {
    renditionRef.current?.display(cfi);
    setShowSidebar(false);
  };

  return (
    <div className="reader">
      <div className="readerToolbar">
        <button
          type="button"
          onClick={() => history.goBack()}
          aria-label="Back"
        >
          ←
        </button>
        <button
          type="button"
          onClick={() => setShowSidebar((s) => !s)}
          aria-label="Contents"
        >
          ☰
        </button>
        <div className="readerToolbarSpacer" />
        <button type="button" onClick={onAddBookmark} aria-label="Bookmark">
          🔖
        </button>
      </div>

      <div className="readerBody">
        {showSidebar && (
          <aside className="readerSidebar">
            <div className="readerSidebarSection">
              <div className="readerSidebarHeader">Contents</div>
              <ul className="readerSidebarList">
                {toc.map((item) => (
                  <li key={item.id}>
                    <button
                      type="button"
                      className="readerSidebarLink"
                      onClick={() => onChapterSelect(item.href)}
                    >
                      {item.label}
                    </button>
                  </li>
                ))}
              </ul>
            </div>
            <div className="readerSidebarSection">
              <div className="readerSidebarHeader">Bookmarks</div>
              {bookmarks.length === 0 && (
                <div className="readerSidebarEmpty">None yet</div>
              )}
              <ul className="readerSidebarList">
                {bookmarks.map((b) => (
                  <li key={b.id} className="readerSidebarItem">
                    <button
                      type="button"
                      className="readerSidebarLink"
                      onClick={() => onGotoBookmark(b.location)}
                    >
                      {new Date(b.createdAt).toLocaleString()}
                    </button>
                    <button
                      type="button"
                      className="readerSidebarDelete"
                      onClick={() => onDeleteBookmark(b.id)}
                      aria-label="Delete bookmark"
                    >
                      ×
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          </aside>
        )}

        <div
          className="readerViewer"
          onTouchStart={onTouchStart}
          onTouchEnd={onTouchEnd}
        >
          <div
            className="readerTapPrev"
            onClick={onPrev}
            aria-label="Previous page"
          />
          <div ref={viewerRef} className="readerEpubHost" />
          <div
            className="readerTapNext"
            onClick={onNext}
            aria-label="Next page"
          />
        </div>
      </div>
    </div>
  );
}

export default EpubReader;
