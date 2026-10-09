import { useState } from 'react'
import { api } from '../api'
import { Notice, PasswordField, useAction } from '../ui.jsx'

export default function ChangePassword() {
  const [f, setF] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' })
  const [ok, setOk] = useState('')
  const [run, err] = useAction()
  const submit = async (e) => {
    e.preventDefault(); setOk('')
    if (f.newPassword !== f.confirmPassword) return
    if (await run(() => api('/auth/change-password', { method: 'POST', body: { currentPassword: f.currentPassword, newPassword: f.newPassword } }))) {
      setF({ currentPassword: '', newPassword: '', confirmPassword: '' }); setOk('Your password has been changed.')
    }
  }
  return <><h1>Change password</h1><section className="panel narrow"><form className="stack" onSubmit={submit}>
    <PasswordField label="Current password" required value={f.currentPassword} onChange={(e) => setF({ ...f, currentPassword: e.target.value })} />
    <PasswordField label="New password (6+ characters)" required minLength={6} value={f.newPassword} onChange={(e) => setF({ ...f, newPassword: e.target.value })} />
    <PasswordField label="Confirm new password" required minLength={6} value={f.confirmPassword} onChange={(e) => setF({ ...f, confirmPassword: e.target.value })} />
    {f.confirmPassword && f.newPassword !== f.confirmPassword && <Notice>Passwords do not match.</Notice>}
    <Notice>{err}</Notice><Notice kind="ok">{ok}</Notice><button className="btn" disabled={f.newPassword !== f.confirmPassword}>Change password</button>
  </form></section></>
}
