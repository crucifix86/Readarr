import PropTypes from 'prop-types';
import React, { Component } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Alert from 'Components/Alert';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import EpubReader from './EpubReader';
import PdfReader from './PdfReader';

class ReaderPage extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isLoading: true,
      error: null,
      bookFile: null,
      progress: null
    };
  }

  componentDidMount() {
    this.load();
  }

  async load() {
    const { bookFileId } = this.props.match.params;

    try {
      const bookFile = await createAjaxRequest({
        url: `/bookfile/${bookFileId}`,
        dataType: 'json'
      }).request;

      let progress = null;
      try {
        progress = await createAjaxRequest({
          url: `/user/me/progress/${bookFileId}`,
          dataType: 'json'
        }).request;
      } catch (e) {
        // 204 (no progress yet) or 400 (global api key) — both fine, just no saved progress
        progress = null;
      }

      this.setState({ isLoading: false, bookFile, progress });
    } catch (error) {
      this.setState({ isLoading: false, error });
    }
  }

  render() {
    const { isLoading, error, bookFile, progress } = this.state;
    const { bookFileId } = this.props.match.params;

    if (isLoading) {
      return (
        <PageContent title="Reader">
          <PageContentBody>
            <LoadingIndicator />
          </PageContentBody>
        </PageContent>
      );
    }

    if (error || !bookFile) {
      return (
        <PageContent title="Reader">
          <PageContentBody>
            <Alert kind={kinds.DANGER}>Unable to load book file {bookFileId}</Alert>
          </PageContentBody>
        </PageContent>
      );
    }

    const ext = (bookFile.path || '').split('.').pop().toLowerCase();
    const contentUrl = `${window.Readarr.apiRoot}/bookfile/${bookFileId}/content?apikey=${encodeURIComponent(window.Readarr.apiKey)}`;

    if (ext === 'epub') {
      return (
        <EpubReader
          bookFileId={Number(bookFileId)}
          contentUrl={contentUrl}
          initialLocation={progress ? progress.location : null}
        />
      );
    }

    if (ext === 'pdf') {
      return (
        <PdfReader
          bookFileId={Number(bookFileId)}
          contentUrl={contentUrl}
          initialLocation={progress ? progress.location : null}
        />
      );
    }

    return (
      <PageContent title="Reader">
        <PageContentBody>
          <Alert kind={kinds.WARNING}>
            Unsupported format: .{ext}. Only epub and pdf are supported in the in-browser reader.
          </Alert>
        </PageContentBody>
      </PageContent>
    );
  }
}

ReaderPage.propTypes = {
  match: PropTypes.shape({
    params: PropTypes.shape({
      bookFileId: PropTypes.string.isRequired
    }).isRequired
  }).isRequired
};

export default ReaderPage;
