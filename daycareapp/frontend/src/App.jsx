import { createContext, useContext, useEffect, useState } from 'react'
import { HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr'
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
import Messages from './pages/Messages.jsx'

const AuthCtx = createContext(null)
export const useAuth = () => useContext(AuthCtx)

const ROLE_LABEL = { super_admin: 'Super admin', admin: 'Center admin', staff: 'Staff', parent: 'Parent' }

// [path, label, roles allowed, component]
const PAGES = [
  ['/centers', 'Centers', ['super_admin'], Centers],
  ['/children', 'Children', ['admin', 'staff'], Children],
  ['/staff', 'Staff', ['admin'], Staff],
  ['/classrooms', 'Classrooms', ['admin'], Classrooms],
  ['/attendance', 'Attendance', ['admin', 'staff', 'parent'], Attendance],
  ['/reports', 'Daily reports', ['admin', 'staff', 'parent'], Reports],
  ['/announcements', 'Announcements', ['admin', 'staff', 'parent'], Announcements],
  ['/messages', 'Messages', ['admin', 'staff', 'parent'], Messages],
]

function Layout() {
  const { user, logout, notifications, unreadNotifications, markNotificationRead } = useAuth()
  const links = PAGES.filter((p) => p[2].includes(user.role))
  return (
    <div className="shell">
      <aside className="side">
        <div className="brand">KidSyncApp</div>
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
          <details className="notification-menu">
            <summary>Notifications {unreadNotifications > 0 ? `(${unreadNotifications})` : ''}</summary>
            <div className="notification-list">
              {notifications.length === 0 ? <span className="muted">No notifications.</span> : notifications.map((item) => (
                <button key={item.id} className={item.readAt ? '' : 'unread-notification'} onClick={() => markNotificationRead(item)}>
                  <strong>{item.title}</strong><span>{item.body}</span>
                </button>
              ))}
            </div>
          </details>
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
  const [notifications, setNotifications] = useState([])

  useEffect(() => {
    if (!user) {
      setNotifications([])
      return
    }
    let active = true
    const token = localStorage.getItem('token')
    const connection = new HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_URL ?? (import.meta.env.DEV ? 'http://localhost:5000' : '')}/hubs/notifications`, {
        accessTokenFactory: () => token,
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .build()
    connection.on('notification', (notification) => {
      if (active) {
        setNotifications((current) => [
          notification,
          ...current.filter((item) => item.id !== notification.id),
        ].slice(0, 50))
        window.dispatchEvent(new CustomEvent('app-notification', { detail: notification }))
      }
    })
    const loadNotifications = () =>
      api('/notifications').then((items) => active && setNotifications(items)).catch(() => {})
    loadNotifications()
    const poll = window.setInterval(loadNotifications, 5000)
    connection.start().catch(() => {})
    return () => {
      active = false
      window.clearInterval(poll)
      connection.stop()
    }
  }, [user])

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

  const markNotificationRead = async (notification) => {
    if (!notification.readAt) {
      await api(`/notifications/${notification.id}/read`, { method: 'POST' })
      setNotifications((current) => current.map((item) =>
        item.id === notification.id ? { ...item, readAt: new Date().toISOString() } : item))
    }
    if (notification.link) window.location.href = notification.link
  }

  const first = PAGES.find((p) => p[2].includes(user?.role))

  return (
    <AuthCtx.Provider value={{
      user, login, logout, notifications, markNotificationRead,
      unreadNotifications: notifications.filter((item) => !item.readAt).length,
    }}>
      <Routes>
        <Route path="/login" element={user ? <Navigate to="/" replace /> : <Login />} />
        <Route path="/forgot-password" element={user ? <Navigate to="/" replace /> : <ForgotPassword />} />
        <Route path="/reset-password" element={user ? <Navigate to="/" replace /> : <ResetPassword />} />
        <Route element={user ? <Layout /> : <Navigate to="/login" replace />}>
          <Route index element={<Navigate to={first ? first[0] : '/login'} replace />} />
          {PAGES.map(([path, , roles, Page]) =>
            roles.includes(user?.role) ? <Route key={path} path={path} element={<Page />} /> : null
          )}
          <Route path="/change-password" element={<ChangePassword />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthCtx.Provider>
  )
}
