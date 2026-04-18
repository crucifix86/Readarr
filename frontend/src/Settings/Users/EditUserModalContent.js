import PropTypes from 'prop-types';
import React from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './EditUserModalContent.css';

const roleOptions = [
  { key: 0, value: 'User' },
  { key: 1, value: 'Admin' }
];

function EditUserModalContent(props) {
  const {
    id,
    isFetching,
    error,
    isSaving,
    saveError,
    item,
    onInputChange,
    onSavePress,
    onModalClose,
    onDeleteUserPress,
    ...otherProps
  } = props;

  const {
    username,
    password,
    role,
    email
  } = item;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {id ? translate('EditUser') : translate('AddUser')}
      </ModalHeader>

      <ModalBody className={styles.body}>
        {
          isFetching &&
            <LoadingIndicator />
        }

        {
          !isFetching && !!error &&
            <div>
              {translate('UnableToAddANewUserPleaseTryAgain')}
            </div>
        }

        {
          !isFetching && !error &&
            <Form {...otherProps}>
              <FormGroup>
                <FormLabel>{translate('Username')}</FormLabel>
                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="username"
                  {...username}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('Password')}</FormLabel>
                <FormInputGroup
                  type={inputTypes.PASSWORD}
                  name="password"
                  helpText={id ? translate('LeaveBlankToKeepCurrent') : ''}
                  {...password}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('Role')}</FormLabel>
                <FormInputGroup
                  type={inputTypes.SELECT}
                  name="role"
                  values={roleOptions}
                  {...role}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('Email')}</FormLabel>
                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="email"
                  {...email}
                  onChange={onInputChange}
                />
              </FormGroup>
            </Form>
        }
      </ModalBody>

      <ModalFooter>
        {
          id &&
            <Button
              className={styles.deleteButton}
              kind={kinds.DANGER}
              onPress={onDeleteUserPress}
            >
              {translate('Delete')}
            </Button>
        }

        <Button onPress={onModalClose}>
          {translate('Cancel')}
        </Button>

        <SpinnerErrorButton
          isSpinning={isSaving}
          error={saveError}
          onPress={onSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

EditUserModalContent.propTypes = {
  id: PropTypes.number,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteUserPress: PropTypes.func
};

export default EditUserModalContent;
