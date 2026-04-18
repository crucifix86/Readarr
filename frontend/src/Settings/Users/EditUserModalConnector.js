import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import EditUserModal from './EditUserModal';

function mapStateToProps() {
  return {};
}

const mapDispatchToProps = {
  clearPendingChanges
};

class EditUserModalConnector extends Component {

  onModalClose = () => {
    this.props.clearPendingChanges({ section: 'settings.users' });
    this.props.onModalClose();
  };

  render() {
    return (
      <EditUserModal
        {...this.props}
        onModalClose={this.onModalClose}
      />
    );
  }
}

EditUserModalConnector.propTypes = {
  onModalClose: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired
};

export default connect(mapStateToProps, mapDispatchToProps)(EditUserModalConnector);
