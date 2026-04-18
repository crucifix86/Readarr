import classNames from 'classnames';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditUserModalConnector from './EditUserModalConnector';
import styles from './User.css';

class User extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isEditUserModalOpen: false,
      isDeleteUserModalOpen: false,
      apiKeyRevealed: false
    };
  }

  onEditUserPress = () => {
    this.setState({ isEditUserModalOpen: true });
  };

  onEditUserModalClose = () => {
    this.setState({ isEditUserModalOpen: false });
  };

  onDeleteUserPress = () => {
    this.setState({
      isEditUserModalOpen: false,
      isDeleteUserModalOpen: true
    });
  };

  onDeleteUserModalClose = () => {
    this.setState({ isDeleteUserModalOpen: false });
  };

  onConfirmDeleteUser = () => {
    this.props.onConfirmDeleteUser(this.props.id);
  };

  onToggleApiKeyReveal = () => {
    this.setState({ apiKeyRevealed: !this.state.apiKeyRevealed });
  };

  onRegenerateApiKeyPress = () => {
    this.props.onRegenerateApiKeyPress(this.props.id);
  };

  render() {
    const {
      id,
      username,
      role,
      email,
      apiKey
    } = this.props;

    const masked = apiKey ? apiKey.slice(0, 4) + '…' + apiKey.slice(-4) : '';

    return (
      <div className={classNames(styles.user)}>
        <div className={styles.username}>{username}</div>
        <div className={styles.role}>{role === 1 || role === 'Admin' ? 'Admin' : 'User'}</div>
        <div className={styles.email}>{email || ''}</div>
        <div className={styles.apiKey}>
          <span className={styles.apiKeyValue}>
            {this.state.apiKeyRevealed ? apiKey : masked}
          </span>
          <Link
            className={styles.apiKeyAction}
            onPress={this.onToggleApiKeyReveal}
            title={this.state.apiKeyRevealed ? translate('Hide') : translate('Show')}
          >
            <Icon name={icons.VIEW} />
          </Link>
          <Link
            className={styles.apiKeyAction}
            onPress={this.onRegenerateApiKeyPress}
            title={translate('Regenerate')}
          >
            <Icon name={icons.REFRESH} />
          </Link>
        </div>

        <div className={styles.actions}>
          <Link onPress={this.onEditUserPress}>
            <Icon name={icons.EDIT} />
          </Link>
        </div>

        <EditUserModalConnector
          id={id}
          isOpen={this.state.isEditUserModalOpen}
          onModalClose={this.onEditUserModalClose}
          onDeleteUserPress={this.onDeleteUserPress}
        />

        <ConfirmModal
          isOpen={this.state.isDeleteUserModalOpen}
          kind={kinds.DANGER}
          title={translate('DeleteUser')}
          message={translate('DeleteUserMessageText', [username])}
          confirmLabel={translate('Delete')}
          onConfirm={this.onConfirmDeleteUser}
          onCancel={this.onDeleteUserModalClose}
        />
      </div>
    );
  }
}

User.propTypes = {
  id: PropTypes.number.isRequired,
  username: PropTypes.string.isRequired,
  role: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
  email: PropTypes.string,
  apiKey: PropTypes.string,
  onConfirmDeleteUser: PropTypes.func.isRequired,
  onRegenerateApiKeyPress: PropTypes.func.isRequired
};

export default User;
