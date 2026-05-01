import type { ReactNode } from 'react';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import styles from './index.module.css';

const sections = [
  {
    emoji: '🚀',
    title: 'Getting Started',
    description:
      'Install Smooth Operator with Docker Compose, complete the first-setup wizard, and invite your first users in minutes.',
    link: '/docs/getting-started',
    linkText: 'Quick Start →',
  },
  {
    emoji: '👤',
    title: 'User Guide',
    description:
      'Learn how to browse your assigned vaults, launch remote sessions, and manage your profile and credentials.',
    link: '/docs/user-guide',
    linkText: 'User Guide →',
  },
  {
    emoji: '🛠️',
    title: 'Admin Guide',
    description:
      'Manage users, groups, vaults, connections, SMTP, SSO (OIDC & SAML), and audit logs as an administrator.',
    link: '/docs/admin-guide',
    linkText: 'Admin Guide →',
  },
  {
    emoji: '📡',
    title: 'API Reference',
    description:
      'Browse the interactive Swagger UI, explore all REST endpoints, and integrate Smooth Operator into your workflows.',
    link: '/docs/api-reference',
    linkText: 'API Reference →',
  },
];

function Hero() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <header className={styles.heroBanner}>
      <div className="container">
        <div className={styles.heroLock}>🔐</div>
        <Heading as="h1" className={styles.heroTitle}>
          {siteConfig.title}
        </Heading>
        <p className={styles.heroSubtitle}>{siteConfig.tagline}</p>
        <p className={styles.heroDescription}>
          A cloud-native, clientless remote access vault. Give your team
          secure browser-based access to SSH, RDP, and VNC servers—without
          VPN clients, exposed ports, or direct credential sharing.
        </p>
        <div className={styles.heroButtons}>
          <Link className="button button--primary button--lg" to="/docs/getting-started">
            Get Started
          </Link>
          <Link className="button button--outline button--secondary button--lg" to="/docs/api-reference/swagger">
            API Reference
          </Link>
          <Link
            className="button button--outline button--secondary button--lg"
            href="http://localhost:4200"
            target="_blank"
          >
            Open App ↗
          </Link>
        </div>
      </div>
    </header>
  );
}

function SectionCard({ emoji, title, description, link, linkText }: (typeof sections)[number]) {
  return (
    <div className={styles.card}>
      <div className={styles.cardEmoji}>{emoji}</div>
      <Heading as="h3" className={styles.cardTitle}>{title}</Heading>
      <p className={styles.cardDescription}>{description}</p>
      <Link className={styles.cardLink} to={link}>{linkText}</Link>
    </div>
  );
}

export default function Home(): ReactNode {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title={siteConfig.title}
      description="Documentation for Smooth Operator — clientless remote access vault for SSH, RDP, and VNC."
    >
      <Hero />
      <main>
        <section className={styles.sectionGrid}>
          <div className="container">
            <div className={styles.grid}>
              {sections.map((s) => (
                <SectionCard key={s.title} {...s} />
              ))}
            </div>
          </div>
        </section>

        <section className={styles.quickLinks}>
          <div className="container">
            <Heading as="h2">Quick Links</Heading>
            <div className={styles.quickLinksGrid}>
              <Link to="/docs/getting-started/installation">📦 Docker Compose setup</Link>
              <Link to="/docs/getting-started/first-setup">🔑 First-access wizard</Link>
              <Link to="/docs/admin-guide/users">👥 Invite users</Link>
              <Link to="/docs/admin-guide/connections">🖥️ Add a connection</Link>
              <Link to="/docs/admin-guide/settings-sso">🔐 Configure SSO</Link>
              <Link to="/docs/api-reference/integration-guide">⚙️ API integration guide</Link>
            </div>
          </div>
        </section>
      </main>
    </Layout>
  );
}
