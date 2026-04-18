import React from 'react';
import { NavLink, useHistory } from 'react-router-dom';
import { useAuth } from '../auth';

interface ShellProps {
  children: React.ReactNode;
}

function Shell(props: ShellProps) {
  const { user, setUser } = useAuth();
  const history = useHistory();

  const onLogout = () => {
    setUser(null);
    history.push('/login');
  };

  return (
    <div className="shell">
      <header className="shellHeader">
        <div className="shellBrand">Readarr Reader</div>
        <nav className="shellNav">
          <NavLink to="/library" activeClassName="active">
            Library
          </NavLink>
          <NavLink to="/reading" activeClassName="active">
            Reading
          </NavLink>
          <NavLink to="/favorites" activeClassName="active">
            Favorites
          </NavLink>
        </nav>
        <div className="shellUser">
          <span className="shellUsername">{user ? user.username : ''}</span>
          <button type="button" className="shellLogout" onClick={onLogout}>
            Logout
          </button>
        </div>
      </header>

      <main className="shellMain">{props.children}</main>
    </div>
  );
}

export default Shell;
