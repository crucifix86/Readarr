import React from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';
import UsersConnector from './UsersConnector';

function UserSettings() {
  return (
    <PageContent title={translate('Users')}>
      <SettingsToolbarConnector
        showSave={false}
      />

      <PageContentBody>
        <UsersConnector />
      </PageContentBody>
    </PageContent>
  );
}

export default UserSettings;
