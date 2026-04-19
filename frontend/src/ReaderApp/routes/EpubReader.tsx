import ePub, { Book, NavItem, Rendition } from 'epubjs';
import React, { useEffect, useMemo, useRef, useState } from 'react';
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

// Flatten the TOC tree so we can show "Chapter X of Y" counting all entries.
function flattenToc(items: NavItem[]): NavItem[] {
  const out: NavItem[] = [];
  const walk = (list: NavItem[]) => {
    for (const item of list) {
      out.push(item);
      if (item.subitems && item.subitems.length) walk(item.subitems);
    }
  };
  walk(items);
  return out;
}

function EpubReader(props: Props) {
  const history = useHistory();
  const viewerRef = useRef<HTMLDivElement>(null);
  const bookRef = useRef<Book | null>(null);
  const renditionRef = useRef<Rendition | null>(null);
  const saveTimerRef = useRef<number | null>(null);

  const [toc, setToc] = useState<NavItem[]>([]);
  const [bookmarks, setBookmarks] = useState<Bookmark[]>([]);
  const [showSidebar, setShowSidebar] = useState(false);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>(
    'loading'
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [currentHref, setCurrentHref] = useState<string | null>(null);
  const [percent, setPercent] = useState<number | null>(null);

  const flatToc = useMemo(() => flattenToc(toc), [toc]);
  const chapterIndex = useMemo(() => {
    if (!currentHref) return -1;
    const bare = currentHref.split('#')[0];
    return flatToc.findIndex((it) => (it.href || '').split('#')[0] === bare);
  }, [flatToc, currentHref]);
  const currentChapter = chapterIndex >= 0 ? flatToc[chapterIndex] : null;

  useEffect(() => {
    const host = viewerRef.current;
    if (!host) return undefined;

    let cancelled = false;
    let rendition: Rendition | null = null;
    const book = ePub(props.contentUrl, { openAs: 'epub' });
    bookRef.current = book;

    const start = () => {
      if (cancelled) return;
      if (host.clientWidth === 0 || host.clientHeight === 0) {
        window.requestAnimationFrame(start);
        return;
      }

      try {
        // Scrolled-doc: each chapter renders as a regular scrollable DOM
        // fragment instead of the finicky paginated iframe. Much more
        // reliable — this is what Kavita / KOReader-web / most robust
        // epub viewers use.
        // scrolled-doc + default manager: each chapter is its own scrollable
        // view. Prev/Next button loads adjacent chapters. Users get clear
        // section boundaries instead of one endless scroll.
        rendition = book.renderTo(host, {
          width: '100%',
          height: '100%',
          flow: 'scrolled-doc',
          manager: 'default',
          allowScriptedContent: false,
        });
        renditionRef.current = rendition;
      } catch (err) {
        setStatus('error');
        setErrorMessage(err instanceof Error ? err.message : String(err));
        return;
      }

      rendition.themes.default({
        body: {
          padding: '24px 32px',
          'max-width': '720px',
          margin: '0 auto',
          'font-family':
            '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, serif',
          'font-size': '16px',
          'line-height': '1.6',
          color: '#222',
        },
        img: { 'max-width': '100%', height: 'auto' },
        a: { color: '#0077cc' },
      });

      (props.initialLocation
        ? rendition.display(props.initialLocation)
        : rendition.display()
      )
        .then(() => {
          if (!cancelled) setStatus('ready');
        })
        .catch(() => {
          // Fallback: display from start if the saved location is bad.
          if (cancelled || !rendition) return;
          rendition
            .display()
            .then(() => !cancelled && setStatus('ready'))
            .catch((err: Error) => {
              if (cancelled) return;
              setStatus('error');
              setErrorMessage(err.message);
            });
        });

      book.loaded.navigation
        .then((nav) => {
          if (!cancelled) setToc(nav.toc || []);
        })
        .catch(() => {
          /* noop */
        });

      rendition.on(
        'relocated',
        (location: {
          start?: { cfi: string; href?: string; percentage?: number };
        }) => {
          if (cancelled || !location?.start) return;
          const cfi = location.start.cfi;
          const href = location.start.href || null;
          const pct =
            typeof location.start.percentage === 'number'
              ? location.start.percentage
              : null;
          setCurrentHref(href);
          setPercent(pct);
          if (saveTimerRef.current) {
            window.clearTimeout(saveTimerRef.current);
          }
          saveTimerRef.current = window.setTimeout(() => {
            apiFetch(props.user, `/user/me/progress/${props.bookFileId}`, {
              method: 'PUT',
              body: JSON.stringify({ location: cfi, percent: pct }),
            }).catch(() => {
              /* noop */
            });
          }, 1200);
        }
      );

      // eslint-disable-next-line no-use-before-define
      loadBookmarks();
    };

    start();

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'ArrowRight' || e.key === 'PageDown') {
        renditionRef.current?.next();
      }
      if (e.key === 'ArrowLeft' || e.key === 'PageUp') {
        renditionRef.current?.prev();
      }
      if (e.key === 'Escape') {
        history.goBack();
      }
    };
    document.addEventListener('keydown', onKeyDown);

    return () => {
      cancelled = true;
      document.removeEventListener('keydown', onKeyDown);
      if (saveTimerRef.current) {
        window.clearTimeout(saveTimerRef.current);
      }
      try {
        renditionRef.current?.destroy();
      } catch {
        /* noop */
      }
      try {
        book.destroy();
      } catch {
        /* noop */
      }
    };
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
        <button
          type="button"
          onClick={onPrev}
          aria-label="Previous chapter"
          title="Previous chapter"
        >
          ◀
        </button>
        <div className="readerChapterLabel">
          {currentChapter ? (
            <>
              <div className="readerChapterTitle">{currentChapter.label}</div>
              {flatToc.length > 0 && (
                <div className="readerChapterCount">
                  Chapter {chapterIndex + 1} of {flatToc.length}
                </div>
              )}
            </>
          ) : (
            <div className="readerChapterTitle">&nbsp;</div>
          )}
        </div>
        <button
          type="button"
          onClick={onNext}
          aria-label="Next chapter"
          title="Next chapter"
        >
          ▶
        </button>
        <div className="readerToolbarSpacer" />
        <button type="button" onClick={onAddBookmark} aria-label="Bookmark">
          🔖
        </button>
      </div>

      <div className="readerProgressTrack">
        <div
          className="readerProgressFill"
          style={{
            width: `${Math.min(
              100,
              Math.max(0, Math.round((percent ?? 0) * 100))
            )}%`,
          }}
        />
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

        <div className="readerViewer">
          {status === 'loading' && (
            <div className="readerStatus">Loading book…</div>
          )}
          {status === 'error' && (
            <div className="readerStatus readerStatusError">
              Unable to render this book.
              {errorMessage ? ` (${errorMessage})` : ''}
            </div>
          )}
          <div ref={viewerRef} className="readerEpubHost" />
        </div>
      </div>
    </div>
  );
}

export default EpubReader;
