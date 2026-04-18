import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditUserModalConnector from './EditUserModalConnector';
import User from './User';
import styles from './Users.css';

class Users extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isAddUserModalOpen: false
    };
  }

  onAddUserPress = () => {
    this.setState({ isAddUserModalOpen: true });
  };

  onModalClose = () => {
    this.setState({ isAddUserModalOpen: false });
  };

  render() {
    const {
      items,
      onConfirmDeleteUser,
      onRegenerateApiKeyPress,
      ...otherProps
    } = this.props;

    return (
      <FieldSet legend={translate('Users')}>
        <PageSectionContent
          errorMessage={translate('UnableToLoadUsers')}
          {...otherProps}
        >
          <div className={styles.usersHeader}>
            <div className={styles.username}>{translate('Username')}</div>
            <div className={styles.role}>{translate('Role')}</div>
            <div className={styles.email}>{translate('Email')}</div>
            <div className={styles.apiKey}>{translate('ApiKey')}</div>
          </div>

          <div>
            {
              items.map((item) => {
                return (
                  <User
                    key={item.id}
                    {...item}
                    onConfirmDeleteUser={onConfirmDeleteUser}
                    onRegenerateApiKeyPress={onRegenerateApiKeyPress}
                  />
                );
              })
            }
          </div>

          <div className={styles.addUser}>
            <Link
              className={styles.addButton}
              onPress={this.onAddUserPress}
            >
              <Icon name={icons.ADD} />
            </Link>
          </div>

          <EditUserModalConnector
            isOpen={this.state.isAddUserModalOpen}
            onModalClose={this.onModalClose}
          />
        </PageSectionContent>
      </FieldSet>
    );
  }
}

Users.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  onConfirmDeleteUser: PropTypes.func.isRequired,
  onRegenerateApiKeyPress: PropTypes.func.isRequired
};

export default Users;
