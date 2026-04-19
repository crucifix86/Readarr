import React, { useEffect, useState } from 'react';
import { useHistory, useParams } from 'react-router-dom';
import { apiFetch, contentUrl } from '../api';
import { useAuth } from '../auth';
import EpubReader from './EpubReader';
import PdfReader from './PdfReader';

interface BookFile {
  id: number;
  path: string;
}

interface Progress {
  location: string | null;
  percent: number | null;
}

function extOf(path: string): string {
  const i = path.lastIndexOf('.');
  return i >= 0 ? path.slice(i + 1).toLowerCase() : '';
}

function Read() {
  const { user } = useAuth();
  const { bookFileId } = useParams<{ bookFileId: string }>();
  const history = useHistory();

  const [bookFile, setBookFile] = useState<BookFile | null>(null);
  const [progress, setProgress] = useState<Progress | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!user) {
      return;
    }

    let cancelled = false;

    (async () => {
      try {
        const bf = await apiFetch<BookFile>(user, `/bookfile/${bookFileId}`);
        const prog = await apiFetch<Progress>(
          user,
          `/user/me/progress/${bookFileId}`
        ).catch(() => null);
        if (!cancelled) {
          setBookFile(bf);
          setProgress(prog);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : String(err));
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [user, bookFileId]);

  if (error) {
    return (
      <div className="pageStatus pageError">
        {error}
        <div>
          <button type="button" onClick={() => history.goBack()}>
            Back
          </button>
        </div>
      </div>
    );
  }

  if (!user || !bookFile) {
    return <div className="pageStatus">Loading book…</div>;
  }

  const ext = extOf(bookFile.path);
  const url = contentUrl(user, Number(bookFileId));

  if (ext === 'epub') {
    return (
      <EpubReader
        user={user}
        bookFileId={Number(bookFileId)}
        initialLocation={progress?.location || null}
      />
    );
  }

  if (ext === 'pdf') {
    return (
      <PdfReader
        user={user}
        bookFileId={Number(bookFileId)}
        contentUrl={url}
        initialLocation={progress?.location || null}
      />
    );
  }

  return <div className="pageStatus">Unsupported file format: .{ext}</div>;
}

export default Read;
