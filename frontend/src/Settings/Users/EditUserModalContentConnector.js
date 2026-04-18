import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { saveUser, setUserValue } from 'Store/Actions/settingsActions';
import selectSettings from 'Store/Selectors/selectSettings';
import EditUserModalContent from './EditUserModalContent';

const newUser = {
  username: '',
  password: '',
  role: 0,
  email: ''
};

function normalizeRole(item) {
  if (item.role === 'Admin') {
    return { ...item, role: 1 };
  }
  if (item.role === 'User') {
    return { ...item, role: 0 };
  }
  return item;
}

function createUserSelector() {
  return createSelector(
    (state, { id }) => id,
    (state) => state.settings.users,
    (id, users) => {
      const {
        isFetching,
        error,
        isSaving,
        saveError,
        pendingChanges,
        items
      } = users;

      const found = id ? _.find(items, { id }) : newUser;
      const base = found ? normalizeRole(found) : newUser;
      const settings = selectSettings(base, pendingChanges, saveError);

      return {
        id,
        isFetching,
        error,
        isSaving,
        saveError,
        item: settings.settings,
        ...settings
      };
    }
  );
}

function createMapStateToProps() {
  return createSelector(
    createUserSelector(),
    (user) => {
      return {
        ...user
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchSetUserValue: setUserValue,
  dispatchSaveUser: saveUser
};

class EditUserModalContentConnector extends Component {

  componentDidMount() {
    if (!this.props.id) {
      Object.keys(newUser).forEach((name) => {
        this.props.dispatchSetUserValue({
          name,
          value: newUser[name]
        });
      });
    }
  }

  componentDidUpdate(prevProps) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }
  }

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetUserValue({ name, value });
  };

  onSavePress = () => {
    this.props.dispatchSaveUser({ id: this.props.id });
  };

  render() {
    return (
      <EditUserModalContent
        {...this.props}
        onSavePress={this.onSavePress}
        onInputChange={this.onInputChange}
      />
    );
  }
}

EditUserModalContentConnector.propTypes = {
  id: PropTypes.number,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  dispatchSetUserValue: PropTypes.func.isRequired,
  dispatchSaveUser: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditUserModalContentConnector);
