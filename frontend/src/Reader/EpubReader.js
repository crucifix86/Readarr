import ePub from 'epubjs';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Reader.css';

class EpubReader extends Component {

  constructor(props, context) {
    super(props, context);

    this.viewerRef = React.createRef();
    this.book = null;
    this.rendition = null;
    this.saveTimer = null;

    this.state = {
      toc: [],
      bookmarks: [],
      showSidebar: false
    };
  }

  componentDidMount() {
    this.book = ePub(this.props.contentUrl, { openAs: 'epub' });
    this.rendition = this.book.renderTo(this.viewerRef.current, {
      width: '100%',
      height: '100%',
      flow: 'paginated',
      manager: 'default'
    });

    const displayPromise = this.props.initialLocation ?
      this.rendition.display(this.props.initialLocation) :
      this.rendition.display();

    displayPromise.catch(() => {
      this.rendition.display();
    });

    this.book.loaded.navigation.then((nav) => {
      this.setState({ toc: nav.toc || [] });
    });

    this.rendition.on('relocated', this.onRelocated);

    this.loadBookmarks();
    document.addEventListener('keydown', this.onKeyDown);
  }

  componentWillUnmount() {
    document.removeEventListener('keydown', this.onKeyDown);

    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    if (this.rendition) {
      this.rendition.destroy();
    }

    if (this.book) {
      this.book.destroy();
    }
  }

  onKeyDown = (e) => {
    if (!this.rendition) {
      return;
    }

    if (e.key === 'ArrowRight' || e.key === 'PageDown') {
      this.rendition.next();
    }

    if (e.key === 'ArrowLeft' || e.key === 'PageUp') {
      this.rendition.prev();
    }
  };

  onRelocated = (location) => {
    if (!location || !location.start) {
      return;
    }

    const cfi = location.start.cfi;
    const percent = typeof location.start.percentage === 'number' ? location.start.percentage : null;

    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    this.saveTimer = setTimeout(() => {
      createAjaxRequest({
        url: `/user/me/progress/${this.props.bookFileId}`,
        method: 'PUT',
        data: JSON.stringify({ location: cfi, percent }),
        dataType: 'json'
      }).request.fail(() => { /* 400 on global-key — ignore */ });
    }, 1200);
  };

  onPrev = () => this.rendition && this.rendition.prev();
  onNext = () => this.rendition && this.rendition.next();

  onToggleSidebar = () => this.setState({ showSidebar: !this.state.showSidebar });

  onChapterSelect = (href) => {
    if (this.rendition) {
      this.rendition.display(href);
      this.setState({ showSidebar: false });
    }
  };

  onAddBookmark = () => {
    if (!this.rendition) {
      return;
    }

    const loc = this.rendition.currentLocation();
    const cfi = loc && loc.start && loc.start.cfi;

    if (!cfi) {
      return;
    }

    createAjaxRequest({
      url: '/user/me/bookmarks',
      method: 'POST',
      data: JSON.stringify({ bookFileId: this.props.bookFileId, location: cfi }),
      dataType: 'json'
    }).request.done(() => this.loadBookmarks());
  };

  onDeleteBookmark = (id) => {
    createAjaxRequest({
      url: `/user/me/bookmarks/${id}`,
      method: 'DELETE'
    }).request.done(() => this.loadBookmarks());
  };

  onGotoBookmark = (cfi) => {
    if (this.rendition) {
      this.rendition.display(cfi);
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
    const { toc, bookmarks, showSidebar } = this.state;

    return (
      <div className={styles.reader}>
        <div className={styles.toolbar}>
          <Link className={styles.toolbarButton} onPress={this.onToggleSidebar}
            title="Table of contents"
          >
            <Icon name={icons.OVERVIEW} />
          </Link>
          <Link className={styles.toolbarButton} onPress={this.onPrev}
            title="Previous"
          >
            <Icon name={icons.ARROW_LEFT} />
          </Link>
          <Link className={styles.toolbarButton} onPress={this.onNext}
            title="Next"
          >
            <Icon name={icons.ARROW_RIGHT} />
          </Link>
          <Link className={styles.toolbarButton} onPress={this.onAddBookmark}
            title="Bookmark current location"
          >
            <Icon name={icons.MONITORED} />
          </Link>
        </div>

        <div className={styles.body}>
          {
            showSidebar &&
              <div className={styles.sidebar}>
                <div className={styles.sidebarSection}>
                  <div className={styles.sidebarHeader}>Contents</div>
                  <ul className={styles.sidebarList}>
                    {
                      toc.map((item) => (
                        <li key={item.id} className={styles.sidebarItem}>
                          <Link onPress={() => this.onChapterSelect(item.href)}>{item.label}</Link>
                        </li>
                      ))
                    }
                  </ul>
                </div>

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
                          <Link onPress={() => this.onGotoBookmark(b.location)}>
                            {new Date(b.createdAt).toLocaleString()}
                          </Link>
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

          <div className={styles.viewer} ref={this.viewerRef} />
        </div>
      </div>
    );
  }
}

EpubReader.propTypes = {
  bookFileId: PropTypes.number.isRequired,
  contentUrl: PropTypes.string.isRequired,
  initialLocation: PropTypes.string
};

export default EpubReader;
