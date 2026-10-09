import { createContext, useContext, useState } from 'react'
import { Navigate, NavLink, Outlet, Route, Routes } from 'react-router-dom'
import { api } from './api'
import Login from './pages/Login.jsx'
import Centers from './pages/Centers.jsx'
import Staff from './pages/Staff.jsx'
import Classrooms from './pages/Classrooms.jsx'
import Children from './pages/Children.jsx'
import Attendance from './pages/Attendance.jsx'
import Reports from './pages/Reports.jsx'
import Announcements from './pages/Announcements.jsx'
import ChangePassword from './pages/ChangePassword.jsx'
import ForgotPassword from './pages/ForgotPassword.jsx'
import ResetPassword from './pages/ResetPassword.jsx'

const AuthCtx = createContext(null)
export const useAuth = () => useContext(AuthCtx)

const ROLE_LABEL = { super_admin: 'Platform owner', admin: 'Daycare admin', staff: 'Teacher', parent: 'Parent' }

// [path, label, roles allowed, component]
const PAGES = [
  ['/centers', 'Daycares', ['super_admin'], Centers],
  ['/children', 'Children', ['admin', 'staff'], Children],
  ['/attendance', 'Attendance', ['admin', 'staff', 'parent'], Attendance],
  ['/reports', 'Daily reports', ['admin', 'staff', 'parent'], Reports],
  ['/announcements', 'Announcements', ['admin', 'staff', 'parent'], Announcements],
  ['/staff', 'Staff', ['admin'], Staff],
  ['/classrooms', 'Classrooms', ['admin'], Classrooms],
]

function Layout() {
  const { user, logout } = useAuth()
  const links = PAGES.filter((p) => p[2].includes(user.role))
  return (
    <div className="shell">
      <aside className="side">
        <div className="brand">DaycarApp</div>
        <nav>
          {links.map(([path, label]) => (
            <NavLink key={path} to={path} className={({ isActive }) => (isActive ? 'active' : '')}>
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="me">
          <strong>{user.fullName}</strong>
          <span>{ROLE_LABEL[user.role]}</span>
          <div className="account-actions">
            <NavLink to="/change-password" className="account-link">Change password</NavLink>
            <button className="btn ghost" onClick={logout}>Sign out</button>
          </div>
        </div>
      </aside>
      <main className="main">
        <Outlet />
      </main>
    </div>
  )
}

export default function App() {
  const [user, setUser] = useState(() => JSON.parse(localStorage.getItem('user') || 'null'))

  const login = async (email, password) => {
    const r = await api('/auth/login', { method: 'POST', body: { email, password } })
    localStorage.setItem('token', r.token)
    localStorage.setItem('user', JSON.stringify(r.user))
    setUser(r.user)
  }
  const logout = () => {
    localStorage.clear()
    setUser(null)
  }

  const first = PAGES.find((p) => p[2].includes(user?.role))

  return (
    <AuthCtx.Provider value={{ user, login, logout }}>
      <Routes>
        <Route path="/login" element={user ? <Navigate to="/" replace /> : <Login />} />
        <Route path="/forgot-password" element={user ? <Navigate to="/" replace /> : <ForgotPassword />} />
        <Route path="/reset-password" element={user ? <Navigate to="/" replace /> : <ResetPassword />} />
        <Route element={user ? <Layout /> : <Navigate to="/login" replace />}>
          <Route index element={<Navigate to={first ? first[0] : '/login'} replace />} />
          {PAGES.map(([path, , roles, Page]) =>
            roles.includes(user?.role) ? <Route key={path} path={path} element={<Page />} /> : null
          )}
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthCtx.Provider>
  )
}
