import { useState } from 'react'
import { api } from '../api'
import { Notice, PasswordField } from '../ui.jsx'

export default function ResetPassword() {
  const token = new URLSearchParams(window.location.search).get('token') || ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const submit = async (event) => {
    event.preventDefault(); setError(''); setMessage('')
    if (password !== confirm) { setError('Passwords do not match.'); return }
    try {
      await api('/auth/reset-password', { method: 'POST', body: { token, newPassword: password } })
      setMessage('Password reset successfully. You can now sign in.')
    } catch (x) { setError(x.message) }
  }
  return <div className="login">
    <form className="panel login-card" onSubmit={submit}>
      <h1>Choose a new password</h1>
      <PasswordField label="New password (6+ characters)" minLength={6} required value={password} onChange={(e) => setPassword(e.target.value)} />
      <PasswordField label="Confirm password" minLength={6} required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      <Notice>{error}</Notice><Notice kind="ok">{message}</Notice>
      <button className="btn" disabled={!token || password !== confirm || Boolean(message)}>Reset password</button>
      <a className="login-link" href="/login">Back to sign in</a>
    </form>
  </div>
}
