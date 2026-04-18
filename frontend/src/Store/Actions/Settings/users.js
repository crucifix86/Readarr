import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { set, update } from 'Store/Actions/baseActions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createRemoveItemHandler from 'Store/Actions/Creators/createRemoveItemHandler';
import createSaveProviderHandler from 'Store/Actions/Creators/createSaveProviderHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';

//
// Variables

const section = 'settings.users';

//
// Actions Types

export const FETCH_USERS = 'settings/users/fetchUsers';
export const SAVE_USER = 'settings/users/saveUser';
export const DELETE_USER = 'settings/users/deleteUser';
export const REGENERATE_USER_API_KEY = 'settings/users/regenerateUserApiKey';
export const SET_USER_VALUE = 'settings/users/setUserValue';

//
// Action Creators

export const fetchUsers = createThunk(FETCH_USERS);
export const saveUser = createThunk(SAVE_USER);
export const deleteUser = createThunk(DELETE_USER);
export const regenerateUserApiKey = createThunk(REGENERATE_USER_API_KEY);

export const setUserValue = createAction(SET_USER_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    items: [],
    isSaving: false,
    saveError: null,
    pendingChanges: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_USERS]: createFetchHandler(section, '/user'),
    [SAVE_USER]: createSaveProviderHandler(section, '/user'),
    [DELETE_USER]: createRemoveItemHandler(section, '/user'),
    [REGENERATE_USER_API_KEY]: function(getState, payload, dispatch) {
      const { id } = payload;

      dispatch(set({ section, isSaving: true }));

      const promise = createAjaxRequest({
        url: `/user/${id}/regenerateApiKey`,
        method: 'POST'
      }).request;

      promise.done((data) => {
        const users = getState().settings.users.items.map((u) => {
          return u.id === data.id ? data : u;
        });

        dispatch(batchActions([
          update({ section, data: users }),
          set({ section, isSaving: false, saveError: null })
        ]));
      });

      promise.fail((xhr) => {
        dispatch(set({ section, isSaving: false, saveError: xhr }));
      });
    }
  },

  //
  // Reducers

  reducers: {
    [SET_USER_VALUE]: createSetSettingValueReducer(section)
  }

};
