import React, { FormEvent, useState } from 'react';
import { Redirect, useHistory } from 'react-router-dom';
import { ApiError, login } from '../api';
import { useAuth } from '../auth';

function Login() {
  const { user, setUser } = useAuth();
  const history = useHistory();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (user) {
    return <Redirect to="/library" />;
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);

    try {
      const result = await login(username.trim(), password);
      setUser(result);
      history.push('/library');
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Login failed');
      }
      setBusy(false);
    }
  };

  return (
    <div className="loginWrap">
      <form className="loginForm" onSubmit={onSubmit}>
        <h1 className="loginTitle">Readarr Reader</h1>
        <p className="loginHint">
          Sign in with the username and password your admin gave you.
        </p>

        <label className="loginLabel" htmlFor="username">
          Username
        </label>
        <input
          id="username"
          className="loginInput"
          autoComplete="username"
          autoCapitalize="none"
          autoCorrect="off"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          disabled={busy}
          required={true}
        />

        <label className="loginLabel" htmlFor="password">
          Password
        </label>
        <input
          id="password"
          className="loginInput"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          disabled={busy}
          required={true}
        />

        {error && <div className="loginError">{error}</div>}

        <button className="loginSubmit" type="submit" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}

export default Login;
