import * as pdfjsLib from 'pdfjs-dist/build/pdf.min.mjs';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Reader.css';

pdfjsLib.GlobalWorkerOptions.workerSrc = `${window.Readarr.urlBase}/Content/pdf.worker.min.mjs`;

class PdfReader extends Component {

  constructor(props, context) {
    super(props, context);

    this.canvasRef = React.createRef();
    this.pdf = null;
    this.saveTimer = null;

    const initialPage = Number(props.initialLocation) > 0 ? Number(props.initialLocation) : 1;

    this.state = {
      page: initialPage,
      totalPages: 0,
      bookmarks: [],
      showSidebar: false,
      rendering: false
    };
  }

  async componentDidMount() {
    document.addEventListener('keydown', this.onKeyDown);

    const loadingTask = pdfjsLib.getDocument({ url: this.props.contentUrl });
    this.pdf = await loadingTask.promise;

    this.setState({ totalPages: this.pdf.numPages });
    this.renderPage(this.state.page);
    this.loadBookmarks();
  }

  componentWillUnmount() {
    document.removeEventListener('keydown', this.onKeyDown);

    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    if (this.pdf) {
      this.pdf.destroy();
    }
  }

  onKeyDown = (e) => {
    if (e.key === 'ArrowRight' || e.key === 'PageDown') {
      this.goTo(this.state.page + 1);
    }

    if (e.key === 'ArrowLeft' || e.key === 'PageUp') {
      this.goTo(this.state.page - 1);
    }
  };

  async renderPage(pageNumber) {
    if (!this.pdf) {
      return;
    }

    this.setState({ rendering: true });

    const page = await this.pdf.getPage(pageNumber);
    const canvas = this.canvasRef.current;
    if (!canvas) {
      return;
    }

    const viewport = page.getViewport({ scale: 1.5 });
    canvas.width = viewport.width;
    canvas.height = viewport.height;

    const ctx = canvas.getContext('2d');
    await page.render({ canvasContext: ctx, viewport, canvas }).promise;

    this.setState({ rendering: false });

    this.scheduleSave(pageNumber);
  }

  scheduleSave(pageNumber) {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    this.saveTimer = setTimeout(() => {
      const percent = this.state.totalPages > 0 ? pageNumber / this.state.totalPages : null;
      createAjaxRequest({
        url: `/user/me/progress/${this.props.bookFileId}`,
        method: 'PUT',
        data: JSON.stringify({ location: String(pageNumber), percent }),
        dataType: 'json'
      }).request.fail(() => { /* 400 on global-key — ignore */ });
    }, 1200);
  }

  goTo = (pageNumber) => {
    if (pageNumber < 1 || pageNumber > this.state.totalPages) {
      return;
    }

    this.setState({ page: pageNumber });
    this.renderPage(pageNumber);
  };

  onPrev = () => this.goTo(this.state.page - 1);
  onNext = () => this.goTo(this.state.page + 1);

  onToggleSidebar = () => this.setState({ showSidebar: !this.state.showSidebar });

  onAddBookmark = () => {
    createAjaxRequest({
      url: '/user/me/bookmarks',
      method: 'POST',
      data: JSON.stringify({ bookFileId: this.props.bookFileId, location: String(this.state.page) }),
      dataType: 'json'
    }).request.done(() => this.loadBookmarks());
  };

  onDeleteBookmark = (id) => {
    createAjaxRequest({
      url: `/user/me/bookmarks/${id}`,
      method: 'DELETE'
    }).request.done(() => this.loadBookmarks());
  };

  onGotoBookmark = (locationString) => {
    const page = Number(locationString);
    if (page > 0) {
      this.goTo(page);
    }
  };

  loadBookmarks() {
    createAjaxRequest({
      url: `/user/me/bookmarks?bookFileId=${this.props.bookFileId}`,
      dataType: 'json'
    }).request.done((data) => this.setState({ bookmarks: data || [] }))
      .fail(() => this.setState({ bookmarks: [] }));
  }

  render() {
    const { page, totalPages, bookmarks, showSidebar, rendering } = this.state;

    return (
      <div className={styles.reader}>
        <div className={styles.toolbar}>
          <Link className={styles.toolbarButton} onPress={this.onToggleSidebar}
            title="Bookmarks"
          >
            <Icon name={icons.MONITORED} />
          </Link>
          <Link className={styles.toolbarButton} onPress={this.onPrev}
            title="Previous"
          >
            <Icon name={icons.ARROW_LEFT} />
          </Link>
          <div className={styles.pageIndicator}>
            {page} / {totalPages || '…'}
          </div>
          <Link className={styles.toolbarButton} onPress={this.onNext}
            title="Next"
          >
            <Icon name={icons.ARROW_RIGHT} />
          </Link>
          <Link className={styles.toolbarButton} onPress={this.onAddBookmark}
            title="Bookmark page"
          >
            <Icon name={icons.ADD} />
          </Link>
        </div>

        <div className={styles.body}>
          {
            showSidebar &&
              <div className={styles.sidebar}>
                <div className={styles.sidebarSection}>
                  <div className={styles.sidebarHeader}>Bookmarks</div>
                  <ul className={styles.sidebarList}>
                    {
                      bookmarks.length === 0 &&
                        <li className={styles.sidebarItemEmpty}>No bookmarks yet</li>
                    }
                    {
                      bookmarks.map((b) => (
                        <li key={b.id} className={styles.sidebarItem}>
                          <Link onPress={() => this.onGotoBookmark(b.location)}>Page {b.location}</Link>
                          <Link className={styles.sidebarDelete} onPress={() => this.onDeleteBookmark(b.id)}>
                            <Icon name={icons.REMOVE} />
                          </Link>
                        </li>
                      ))
                    }
                  </ul>
                </div>
              </div>
          }

          <div className={styles.viewer}>
            {rendering && <div className={styles.rendering}>Rendering…</div>}
            <canvas ref={this.canvasRef} className={styles.pdfCanvas} />
          </div>
        </div>
      </div>
    );
  }
}

PdfReader.propTypes = {
  bookFileId: PropTypes.number.isRequired,
  contentUrl: PropTypes.string.isRequired,
  initialLocation: PropTypes.string
};

export default PdfReader;
