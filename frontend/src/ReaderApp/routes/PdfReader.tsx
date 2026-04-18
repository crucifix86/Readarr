import * as pdfjsLib from 'pdfjs-dist/build/pdf.min.mjs';
import React, { useEffect, useRef, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { apiFetch, urlBase } from '../api';
import { ReaderUser } from '../auth';

pdfjsLib.GlobalWorkerOptions.workerSrc = `${urlBase()}/Content/pdf.worker.min.mjs`;

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

const SWIPE_MIN_PX = 40;

function PdfReader(props: Props) {
  const history = useHistory();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const pdfRef = useRef<pdfjsLib.PDFDocumentProxy | null>(null);
  const saveTimerRef = useRef<number | null>(null);
  const touchStartX = useRef<number | null>(null);

  const initialPage =
    Number(props.initialLocation) > 0 ? Number(props.initialLocation) : 1;

  const [page, setPage] = useState<number>(initialPage);
  const [totalPages, setTotalPages] = useState(0);
  const [rendering, setRendering] = useState(false);
  const [bookmarks, setBookmarks] = useState<Bookmark[]>([]);
  const [showSidebar, setShowSidebar] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const task = pdfjsLib.getDocument({ url: props.contentUrl });

    task.promise
      .then((pdf: pdfjsLib.PDFDocumentProxy) => {
        if (cancelled) {
          pdf.destroy();
          return;
        }
        pdfRef.current = pdf;
        setTotalPages(pdf.numPages);
        // eslint-disable-next-line no-use-before-define
        renderPage(initialPage, pdf);
      })
      .catch(() => {
        /* ignore */
      });

    // eslint-disable-next-line no-use-before-define
    loadBookmarks();

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'ArrowRight' || e.key === 'PageDown') {
        // eslint-disable-next-line no-use-before-define
        goTo(page + 1);
      }
      if (e.key === 'ArrowLeft' || e.key === 'PageUp') {
        // eslint-disable-next-line no-use-before-define
        goTo(page - 1);
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
      pdfRef.current?.destroy();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.bookFileId, props.contentUrl]);

  async function renderPage(
    pageNumber: number,
    pdf?: pdfjsLib.PDFDocumentProxy
  ) {
    const doc = pdf || pdfRef.current;
    if (!doc) {
      return;
    }
    setRendering(true);
    const p = await doc.getPage(pageNumber);
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }
    const container = canvas.parentElement as HTMLElement | null;
    const maxWidth = container ? container.clientWidth - 32 : 800;
    const unscaled = p.getViewport({ scale: 1 });
    const scale = Math.min(2.5, Math.max(1, maxWidth / unscaled.width));
    const viewport = p.getViewport({ scale });
    canvas.width = viewport.width;
    canvas.height = viewport.height;
    const ctx = canvas.getContext('2d');
    if (ctx) {
      await p.render({ canvasContext: ctx, viewport, canvas }).promise;
    }
    setRendering(false);

    const percent = totalPages > 0 ? pageNumber / totalPages : null;
    if (saveTimerRef.current) {
      window.clearTimeout(saveTimerRef.current);
    }
    saveTimerRef.current = window.setTimeout(() => {
      apiFetch(props.user, `/user/me/progress/${props.bookFileId}`, {
        method: 'PUT',
        body: JSON.stringify({ location: String(pageNumber), percent }),
      }).catch(() => {
        /* noop */
      });
    }, 1200);
  }

  function loadBookmarks() {
    apiFetch<Bookmark[]>(
      props.user,
      `/user/me/bookmarks?bookFileId=${props.bookFileId}`
    )
      .then((data) => setBookmarks(data || []))
      .catch(() => setBookmarks([]));
  }

  const goTo = (n: number) => {
    if (n < 1 || n > totalPages) {
      return;
    }
    setPage(n);
    renderPage(n);
  };

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
      goTo(page + 1);
    } else {
      goTo(page - 1);
    }
  };

  const onAddBookmark = async () => {
    await apiFetch(props.user, '/user/me/bookmarks', {
      method: 'POST',
      body: JSON.stringify({
        bookFileId: props.bookFileId,
        location: String(page),
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
          aria-label="Bookmarks"
        >
          ☰
        </button>
        <div className="readerToolbarSpacer" />
        <div className="readerPageindicator">
          {page} / {totalPages || '…'}
        </div>
        <button
          type="button"
          onClick={onAddBookmark}
          aria-label="Bookmark page"
        >
          🔖
        </button>
      </div>

      <div className="readerBody">
        {showSidebar && (
          <aside className="readerSidebar">
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
                      onClick={() => {
                        goTo(Number(b.location));
                        setShowSidebar(false);
                      }}
                    >
                      Page {b.location}
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
          className="readerViewer readerPdfViewer"
          onTouchStart={onTouchStart}
          onTouchEnd={onTouchEnd}
        >
          <div
            className="readerTapPrev"
            onClick={() => goTo(page - 1)}
            aria-label="Previous page"
          />
          <canvas ref={canvasRef} className="readerPdfCanvas" />
          <div
            className="readerTapNext"
            onClick={() => goTo(page + 1)}
            aria-label="Next page"
          />
          {rendering && <div className="readerRendering">Rendering…</div>}
        </div>
      </div>
    </div>
  );
}

export default PdfReader;
