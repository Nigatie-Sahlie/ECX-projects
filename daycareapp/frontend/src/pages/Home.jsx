import { Link } from 'react-router-dom'

const FEATURES = [
  {
    title: 'Live attendance tracking',
    detail: 'Check in and check out children with real-time visibility for staff and parents.',
  },
  {
    title: 'Secure family messaging',
    detail: 'Keep classroom updates and private conversations in one secure place.',
  },
  {
    title: 'Daily reports and updates',
    detail: 'Share meals, naps, activities, and important notes without paperwork.',
  },
]

export default function Home() {
  return (
    <div className="guest-home">
      <header className="guest-hero panel">
        <p className="guest-kicker">Welcome to KidSyncApp</p>
        <h1>Run your daycare confidently, keep every family connected.</h1>
        <p className="guest-subtitle">
          KidSyncApp helps daycare teams simplify attendance, communication, and daily care updates.
          Families stay informed, and staff spends less time on admin work.
        </p>
        <div className="guest-actions">
          <Link className="btn" to="/login">Sign in</Link>
          <a className="btn ghost" href="#register">Start registration</a>
        </div>
      </header>

      <section className="guest-features">
        {FEATURES.map((feature) => (
          <article className="panel guest-feature" key={feature.title}>
            <h2>{feature.title}</h2>
            <p className="muted">{feature.detail}</p>
          </article>
        ))}
      </section>

      <section className="panel guest-register" id="register">
        <h2>Ready to register your daycare?</h2>
        <p>
          Ask your platform administrator for a daycare account setup, then sign in and complete your center onboarding.
        </p>
        <Link className="btn" to="/login">Go to sign in</Link>
      </section>
    </div>
  )
}
