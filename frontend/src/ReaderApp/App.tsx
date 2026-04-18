import React, { useMemo, useState } from 'react';
import { BrowserRouter, Redirect, Route, Switch } from 'react-router-dom';
import { urlBase } from './api';
import { AuthContext, loadStoredUser, ReaderUser, storeUser } from './auth';
import Shell from './components/Shell';
import CurrentlyReading from './routes/CurrentlyReading';
import Favorites from './routes/Favorites';
import Library from './routes/Library';
import Login from './routes/Login';
import Read from './routes/Read';

function App() {
  const [user, setUserState] = useState<ReaderUser | null>(() =>
    loadStoredUser()
  );

  const setUser = (next: ReaderUser | null) => {
    storeUser(next);
    setUserState(next);
  };

  const authValue = useMemo(() => ({ user, setUser }), [user]);

  const basename = `${urlBase()}/reader`;

  return (
    <AuthContext.Provider value={authValue}>
      <BrowserRouter basename={basename}>
        <Switch>
          <Route exact={true} path="/login" component={Login} />

          <Route
            path="/"
            render={() => {
              if (!user) {
                return <Redirect to="/login" />;
              }

              return (
                <Shell>
                  <Switch>
                    <Route
                      exact={true}
                      path="/"
                      render={() => <Redirect to="/library" />}
                    />
                    <Route exact={true} path="/library" component={Library} />
                    <Route
                      exact={true}
                      path="/favorites"
                      component={Favorites}
                    />
                    <Route
                      exact={true}
                      path="/reading"
                      component={CurrentlyReading}
                    />
                    <Route
                      exact={true}
                      path="/read/:bookFileId"
                      component={Read}
                    />
                    <Route render={() => <Redirect to="/library" />} />
                  </Switch>
                </Shell>
              );
            }}
          />
        </Switch>
      </BrowserRouter>
    </AuthContext.Provider>
  );
}

export default App;
