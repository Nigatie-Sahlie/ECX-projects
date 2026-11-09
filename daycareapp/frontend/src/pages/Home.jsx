import { useState } from 'react'
import { Link } from 'react-router-dom'

const FEATURES = [
  {
    icon: 'badge',
    title: 'Live attendance tracking',
    detail: 'Check in and check out children with real-time visibility for staff and parents, automated room ratios, and pickup verification.',
    label: 'Active room audit',
    note: 'Real-time cloud log',
  },
  {
    icon: 'forum',
    title: 'Secure family messaging',
    detail: 'Keep classroom updates, photo moments, emergency announcements, and private conversations in one FERPA-ready space.',
    label: 'Encrypted parent feed',
    note: 'Instant push',
  },
  {
    icon: 'auto_stories',
    title: 'Daily reports & updates',
    detail: 'Share meals, nap times, diaper changes, and milestone observations without paperwork or end-of-day scrambles.',
    label: 'One-tap loggers',
    note: 'Zero clipboards',
  },
]

function Logo() {
  return <div className="portal-logo"><span className="portal-logo-mark">K</span><strong>KidSyncApp</strong></div>
}

function AttendanceWave() {
  return (
    <svg className="attendance-wave" viewBox="0 0 600 160" role="img" aria-label="Weekly attendance trend">
      <defs>
        <linearGradient id="attendanceGlow" x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor="currentColor" stopOpacity=".3" />
          <stop offset="100%" stopColor="currentColor" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d="M0,120 Q75,60 150,85 T300,50 T450,70 T600,30 L600,160 L0,160 Z" fill="url(#attendanceGlow)" />
      <path d="M0,120 Q75,60 150,85 T300,50 T450,70 T600,30" fill="none" stroke="currentColor" strokeLinecap="round" strokeWidth="4" />
      {[['150', '85'], ['300', '50'], ['450', '70']].map(([cx, cy]) => <circle key={cx} cx={cx} cy={cy} fill="currentColor" r="5" />)}
      <circle cx="600" cy="30" fill="#fea619" r="6" />
    </svg>
  )
}

export default function Home() {
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <div className="portal">
      <header className="portal-header">
        <Logo />
        <nav className={`portal-nav ${menuOpen ? 'mobile-open' : ''}`} aria-label="Public navigation">
          <a className="active" href="#overview" onClick={() => setMenuOpen(false)}>Overview</a>
          <a href="#features" onClick={() => setMenuOpen(false)}>Daycare portal</a>
          <a href="#enrollment" onClick={() => setMenuOpen(false)}>Enrollment</a>
          <a href="#families" onClick={() => setMenuOpen(false)}>Parents</a>
        </nav>
        <div className="portal-header-actions">
          <Link className="btn" to="/login">Admin login <span aria-hidden="true">→</span></Link>
          <span className="portal-profile" aria-hidden="true">✦</span>
        </div>
        <button className="portal-menu" type="button" aria-label={menuOpen ? 'Close navigation' : 'Open navigation'} aria-expanded={menuOpen} onClick={() => setMenuOpen((open) => !open)}>☰</button>
      </header>

      <main>
        <section className="portal-hero" id="overview">
          <div className="portal-hero-copy">
            <span className="portal-eyebrow"><i /> Welcome to KidSyncApp · Daycare &amp; early learning</span>
            <h1>Run your daycare confidently, keep every family connected.</h1>
            <p>KidSyncApp helps early childcare educators streamline live attendance, parent communication, and developmental milestones in one trusted hub. Families stay delighted, and teachers regain hours every week.</p>
            <div className="portal-actions">
              <Link className="btn" to="/login"><span aria-hidden="true">↪</span> Sign in to portal</Link>
              <a className="btn ghost" href="#enrollment">Start registration <span aria-hidden="true">→</span></a>
            </div>
            <div className="portal-trust">
              <span>♧ HIPAA &amp; FERPA aligned</span>
              <span>☆ 99.4% family satisfaction</span>
              <span>♧ &lt; 3s contactless check-in</span>
            </div>
          </div>
          <div className="portal-hero-art" aria-label="Childcare operations preview">
            <div className="hero-art-glow" />
            <div className="hero-art-card">
              <span className="hero-art-badge">✣ Instant pickup sync<small>Verified guardian pin</small></span>
              <div className="hero-art-scene"><span>☀</span><span>✦</span><span>◡</span></div>
              <div className="hero-art-caption"><span>◉</span><div><strong>Sunflower Toddler Room</strong><small>18 of 18 checked-in safely</small></div></div>
            </div>
            <div className="hero-reassurance"><strong>♡</strong><div><b>99.4%</b><small>Parent reassurance</small></div></div>
          </div>
        </section>

        <section className="portal-section" id="features">
          <div className="portal-section-heading">
            <div><span className="portal-eyebrow plain">Engineered for harmony</span><h2>Everything your center needs to thrive.</h2></div>
            <p>Purpose-built tools designed to minimize daily friction between paperwork and heartfelt childcare.</p>
          </div>
          <div className="feature-grid">
            {FEATURES.map((feature) => <article className="portal-feature" key={feature.title}>
              <span className="feature-icon material-symbols-outlined">{feature.icon}</span>
              <div><h3>{feature.title}</h3><p>{feature.detail}</p></div>
              <footer><span><i /> {feature.label}</span><small>{feature.note}</small></footer>
            </article>)}
          </div>
        </section>

        <section className="snapshot" id="families">
          <div className="snapshot-copy">
            <span className="portal-eyebrow plain">Live classroom snapshot</span>
            <h2>Clear visibility across all classrooms</h2>
            <p>Directors and lead teachers monitor ratio compliance and safety thresholds in real time on any device.</p>
            <div className="room-statuses">
              <div><span><i /> Infants · Nest A</span><b>1:4 ratio · Compliant</b></div>
              <div><span><i /> Toddlers · Discovery B</span><b>1:6 ratio · Compliant</b></div>
              <div className="warm"><span><i /> Pre-K · Explorers C</span><b>Capacity 95%</b></div>
            </div>
          </div>
          <div className="chart-card">
            <div><strong>Weekly attendance average</strong><b>98.2% present</b></div>
            <AttendanceWave />
            <footer><span>Mon</span><span>Tue</span><span>Wed</span><span>Thu</span><span>Fri (Peak)</span></footer>
          </div>
        </section>

        <section className="portal-cta" id="enrollment">
          <span className="portal-eyebrow amber">Get started today</span>
          <h2>Ready to register your daycare?</h2>
          <p>Ask your platform administrator for a daycare account setup, then sign in to complete your center onboarding, class roster imports, and guardian invitations.</p>
          <div className="portal-actions"><Link className="btn" to="/login">Go to sign in</Link><button className="btn soft" type="button">Contact support team</button><small>◷ Setup takes less than 15 minutes</small></div>
        </section>

        <section className="testimonials" aria-label="Customer testimonials">
          <blockquote><span>👩‍👧</span><p>“Morning check-in queues used to take 25 minutes. With KidSyncApp, parents scan and confirm in seconds.”<small>Elena Martinez<br />Director, Little Oaks Academy</small></p></blockquote>
          <blockquote><span>👨‍👦</span><p>“Getting realtime updates during the workday gives my wife and me complete peace of mind.”<small>David Chen<br />Parent of a 3-year-old toddler</small></p></blockquote>
        </section>
      </main>
      <footer className="portal-footer"><Logo /><span>© 2024 KidSyncApp Childcare Operations Platform. All rights reserved.</span><nav><a href="#overview">Safety standards</a><a href="#overview">Privacy policy</a><a href="#overview">Support</a></nav></footer>
    </div>
  )
}
