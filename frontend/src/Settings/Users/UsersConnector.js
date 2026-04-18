import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { deleteUser, fetchUsers, regenerateUserApiKey } from 'Store/Actions/settingsActions';
import Users from './Users';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.users,
    (users) => {
      return {
        ...users
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchUsers: fetchUsers,
  dispatchDeleteUser: deleteUser,
  dispatchRegenerateUserApiKey: regenerateUserApiKey
};

class UsersConnector extends Component {

  componentDidMount() {
    this.props.dispatchFetchUsers();
  }

  onConfirmDeleteUser = (id) => {
    this.props.dispatchDeleteUser({ id });
  };

  onRegenerateApiKeyPress = (id) => {
    this.props.dispatchRegenerateUserApiKey({ id });
  };

  render() {
    return (
      <Users
        {...this.props}
        onConfirmDeleteUser={this.onConfirmDeleteUser}
        onRegenerateApiKeyPress={this.onRegenerateApiKeyPress}
      />
    );
  }
}

UsersConnector.propTypes = {
  dispatchFetchUsers: PropTypes.func.isRequired,
  dispatchDeleteUser: PropTypes.func.isRequired,
  dispatchRegenerateUserApiKey: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(UsersConnector);
