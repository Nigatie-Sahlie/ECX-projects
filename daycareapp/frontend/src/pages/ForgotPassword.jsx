import { useState } from 'react'
import { api } from '../api'
import { Field, Notice } from '../ui.jsx'

export default function ForgotPassword() {
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const submit = async (event) => {
    event.preventDefault(); setError(''); setMessage('')
    try {
      const result = await api('/auth/forgot-password', { method: 'POST', body: { email } })
      setMessage(result.message)
    } catch (x) { setError(x.message) }
  }
  return <div className="login">
    <form className="panel login-card" onSubmit={submit}>
      <h1>Reset password</h1>
      <p className="muted">Enter your account email and we will send a secure reset link.</p>
      <Field label="Email"><input type="email" required autoFocus value={email} onChange={(e) => setEmail(e.target.value)} /></Field>
      <Notice>{error}</Notice><Notice kind="ok">{message}</Notice>
      <button className="btn">Send reset link</button>
      <a className="login-link" href="/login">Back to sign in</a>
    </form>
  </div>
}
