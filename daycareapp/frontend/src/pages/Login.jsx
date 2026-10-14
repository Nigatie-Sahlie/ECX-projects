import { useState } from 'react'
import { useAuth } from '../App.jsx'
import { Field, Notice, PasswordField } from '../ui.jsx'

export default function Login() {
  const { login } = useAuth()
  const [f, setF] = useState({ email: '', password: '' })
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (e) => {
    e.preventDefault()
    setBusy(true)
    setErr('')
    try {
      await login(f.email, f.password)
    } catch (x) {
      setErr(x.message.startsWith('Something went wrong') ? 'Email or password is incorrect.' : x.message)
    }
    setBusy(false)
  }

  return (
    <div className="login">
      <form className="panel login-card" onSubmit={submit}>
        <h1>KidSyncApp</h1>
        <p className="muted">Sign in to see today at your daycare.</p>
        <Field label="Email">
          <input type="email" required autoFocus value={f.email} onChange={(e) => setF({ ...f, email: e.target.value })} />
        </Field>
        <PasswordField label="Password" required value={f.password} onChange={(e) => setF({ ...f, password: e.target.value })} />
        <div className="login-links"><a href="/forgot-password">Forgot password?</a></div>
        <Notice>{err}</Notice>
        <button className="btn" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </div>
  )
}
