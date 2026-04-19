import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useHistory } from 'react-router-dom';
import { apiFetch, urlBase } from '../api';
import { ReaderUser } from '../auth';

interface ChapterResponse {
  title: string;
  page: number;
  part: string | null;
  children: ChapterResponse[] | null;
}

interface InfoResponse {
  bookFileId: number;
  title: string;
  author: string;
  pageCount: number;
}

interface FlatChapter {
  title: string;
  page: number;
  depth: number;
}

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
  initialLocation: string | null;
}

function flattenChapters(
  items: ChapterResponse[] | null,
  depth = 0,
  out: FlatChapter[] = []
): FlatChapter[] {
  if (!items) return out;
  for (const item of items) {
    out.push({ title: item.title || '', page: item.page, depth });
    if (item.children && item.children.length) {
      flattenChapters(item.children, depth + 1, out);
    }
  }
  return out;
}

function parseInitialPage(initial: string | null): number {
  if (!initial) return 0;
  const m = initial.match(/^page:(\d+)/);
  if (m) return parseInt(m[1]);
  const n = parseInt(initial);
  return Number.isFinite(n) && n >= 0 ? n : 0;
}

function EpubReader(props: Props) {
  const history = useHistory();
  const hostRef = useRef<HTMLDivElement>(null);
  const saveTimerRef = useRef<number | null>(null);

  const [info, setInfo] = useState<InfoResponse | null>(null);
  const [chapters, setChapters] = useState<ChapterResponse[]>([]);
  const [page, setPage] = useState<number>(
    parseInitialPage(props.initialLocation)
  );
  const [pageHtml, setPageHtml] = useState<string>('');
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>(
    'loading'
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [bookmarks, setBookmarks] = useState<Bookmark[]>([]);
  const [showSidebar, setShowSidebar] = useState(false);

  const flat = useMemo(() => flattenChapters(chapters), [chapters]);
  const currentChapter = useMemo(() => {
    if (flat.length === 0) return null;
    let best: FlatChapter | null = null;
    for (const c of flat) {
      if (c.page <= page) best = c;
      else break;
    }
    return best || flat[0];
  }, [flat, page]);
  const chapterOrdinal = useMemo(() => {
    if (!currentChapter) return 0;
    return flat.findIndex((c) => c === currentChapter) + 1;
  }, [flat, currentChapter]);

  const pageCount = info?.pageCount ?? 0;
  const percent = pageCount > 0 ? (page + 1) / pageCount : 0;

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      apiFetch<InfoResponse>(
        props.user,
        `/user/me/epub/${props.bookFileId}/info`
      ),
      apiFetch<ChapterResponse[]>(
        props.user,
        `/user/me/epub/${props.bookFileId}/chapters`
      ).catch(() => [] as ChapterResponse[]),
    ])
      .then(([i, ch]) => {
        if (cancelled) return;
        setInfo(i);
        setChapters(ch || []);
      })
      .catch((err) => {
        if (cancelled) return;
        setStatus('error');
        setErrorMessage(err instanceof Error ? err.message : String(err));
      });
    return () => {
      cancelled = true;
    };
  }, [props.user, props.bookFileId]);

  useEffect(() => {
    if (!info) return undefined;
    let cancelled = false;
    setStatus('loading');
    const base = urlBase();
    fetch(`${base}/api/v1/user/me/epub/${props.bookFileId}/page/${page}`, {
      headers: { 'X-Api-Key': props.user.apiKey },
    })
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.text();
      })
      .then((html) => {
        if (cancelled) return;
        setPageHtml(html);
        setStatus('ready');
        if (hostRef.current) {
          hostRef.current.scrollTop = 0;
        }
      })
      .catch((err) => {
        if (cancelled) return;
        setStatus('error');
        setErrorMessage(err instanceof Error ? err.message : String(err));
      });

    if (saveTimerRef.current) window.clearTimeout(saveTimerRef.current);
    saveTimerRef.current = window.setTimeout(() => {
      const pct = info.pageCount > 0 ? (page + 1) / info.pageCount : null;
      apiFetch(props.user, `/user/me/progress/${props.bookFileId}`, {
        method: 'PUT',
        body: JSON.stringify({ location: `page:${page}`, percent: pct }),
      }).catch(() => {
        /* noop */
      });
    }, 800);

    return () => {
      cancelled = true;
    };
  }, [info, page, props.bookFileId, props.user]);

  const loadBookmarks = useCallback(() => {
    apiFetch<Bookmark[]>(
      props.user,
      `/user/me/bookmarks?bookFileId=${props.bookFileId}`
    )
      .then((data) => setBookmarks(data || []))
      .catch(() => setBookmarks([]));
  }, [props.user, props.bookFileId]);

  useEffect(() => {
    loadBookmarks();
  }, [loadBookmarks]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'ArrowRight' || e.key === 'PageDown') {
        setPage((p) => (pageCount > 0 ? Math.min(p + 1, pageCount - 1) : p));
      } else if (e.key === 'ArrowLeft' || e.key === 'PageUp') {
        setPage((p) => Math.max(p - 1, 0));
      } else if (e.key === 'Escape') {
        history.goBack();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [pageCount, history]);

  useEffect(() => {
    const host = hostRef.current;
    if (!host) return undefined;
    const onClick = (e: Event) => {
      const target = e.target as HTMLElement | null;
      if (!target) return;
      const a = target.closest('a[data-epub-href]') as HTMLAnchorElement | null;
      if (!a) return;
      e.preventDefault();
      const href = a.getAttribute('data-epub-href') || '';
      if (!href) return;
      const match = flat.find((c) => href.endsWith(c.title));
      if (match) setPage(match.page);
    };
    host.addEventListener('click', onClick);
    return () => host.removeEventListener('click', onClick);
  }, [flat]);

  const onPrev = () => setPage((p) => Math.max(p - 1, 0));
  const onNext = () =>
    setPage((p) => (pageCount > 0 ? Math.min(p + 1, pageCount - 1) : p));
  const onPrevChapter = () => {
    if (!currentChapter) return;
    const idx = flat.findIndex((c) => c === currentChapter);
    if (idx > 0) setPage(flat[idx - 1].page);
  };
  const onNextChapter = () => {
    if (!currentChapter) return;
    const idx = flat.findIndex((c) => c === currentChapter);
    if (idx >= 0 && idx < flat.length - 1) setPage(flat[idx + 1].page);
  };

  const onChapterSelect = (p: number) => {
    setPage(p);
    setShowSidebar(false);
  };

  const onAddBookmark = async () => {
    await apiFetch(props.user, '/user/me/bookmarks', {
      method: 'POST',
      body: JSON.stringify({
        bookFileId: props.bookFileId,
        location: `page:${page}`,
      }),
    });
    loadBookmarks();
  };

  const onDeleteBookmark = async (id: number) => {
    await apiFetch(props.user, `/user/me/bookmarks/${id}`, {
      method: 'DELETE',
    });
    loadBookmarks();
  };

  const onGotoBookmark = (loc: string) => {
    setPage(parseInitialPage(loc));
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
          onClick={onPrevChapter}
          title="Previous chapter"
          aria-label="Previous chapter"
        >
          ⏮
        </button>
        <button
          type="button"
          onClick={onPrev}
          title="Previous page"
          aria-label="Previous page"
        >
          ◀
        </button>
        <div className="readerChapterLabel">
          {currentChapter && (
            <>
              <div className="readerChapterTitle">{currentChapter.title}</div>
              {flat.length > 0 && (
                <div className="readerChapterCount">
                  Chapter {chapterOrdinal} of {flat.length} · page {page + 1}
                  {pageCount > 0 ? ` / ${pageCount}` : ''}
                </div>
              )}
            </>
          )}
          {!currentChapter && pageCount > 0 && (
            <div className="readerChapterCount">
              Page {page + 1} of {pageCount}
            </div>
          )}
        </div>
        <button
          type="button"
          onClick={onNext}
          title="Next page"
          aria-label="Next page"
        >
          ▶
        </button>
        <button
          type="button"
          onClick={onNextChapter}
          title="Next chapter"
          aria-label="Next chapter"
        >
          ⏭
        </button>
        <div className="readerToolbarSpacer" />
        <button type="button" onClick={onAddBookmark} aria-label="Bookmark">
          🔖
        </button>
      </div>

      <div className="readerProgressTrack">
        <div
          className="readerProgressFill"
          style={{ width: `${Math.min(100, Math.round(percent * 100))}%` }}
        />
      </div>

      <div className="readerBody">
        {showSidebar && (
          <aside className="readerSidebar">
            <div className="readerSidebarSection">
              <div className="readerSidebarHeader">Contents</div>
              {flat.length === 0 && (
                <div className="readerSidebarEmpty">No chapters</div>
              )}
              <ul className="readerSidebarList">
                {flat.map((c, i) => (
                  <li key={i}>
                    <button
                      type="button"
                      className="readerSidebarLink"
                      style={{ paddingLeft: `${8 + c.depth * 12}px` }}
                      onClick={() => onChapterSelect(c.page)}
                    >
                      {c.title || `Section ${c.page + 1}`}
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

        <div ref={hostRef} className="readerViewer">
          {status === 'error' && (
            <div className="readerInlineError">
              Unable to render this page.
              {errorMessage ? ` (${errorMessage})` : ''}
            </div>
          )}
          {status === 'loading' && (
            <div className="readerPageSpinner" aria-label="Loading page" />
          )}
          <div
            className="readerEpubHost"
            // eslint-disable-next-line react/no-danger
            dangerouslySetInnerHTML={{ __html: pageHtml }}
          />
        </div>
      </div>
    </div>
  );
}

export default EpubReader;
