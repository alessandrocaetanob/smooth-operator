import { themes as prismThemes } from 'prism-react-renderer';
import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'Smooth Operator Docs',
  tagline: 'Clientless remote access, done right.',
  favicon: 'img/favicon.ico',

  future: {
    v4: true,
  },

  markdown: {
    format: 'mdx',
  },

  url: 'http://localhost:3000',
  baseUrl: '/',

  organizationName: 'alessandrocaetanob',
  projectName: 'smooth-operator',

  onBrokenLinks: 'warn',
  onBrokenMarkdownLinks: 'warn',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  plugins: [
    [
      require.resolve('@easyops-cn/docusaurus-search-local'),
      {
        hashed: true,
        language: ['en'],
        highlightSearchTermsOnTargetPage: true,
        explicitSearchResultPath: true,
      },
    ],
  ],

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          editUrl:
            'https://github.com/alessandrocaetanob/smooth-operator/tree/main/docs/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/social-card.png',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'Smooth Operator',
      logo: {
        alt: 'Smooth Operator Logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          to: '/docs/getting-started',
          label: '🚀 Getting Started',
          position: 'left',
        },
        {
          to: '/docs/user-guide',
          label: '👤 User Guide',
          position: 'left',
        },
        {
          to: '/docs/admin-guide',
          label: '🛠️ Admin Guide',
          position: 'left',
        },
        {
          to: '/docs/api-reference',
          label: '📡 API Reference',
          position: 'left',
        },
        {
          href: 'https://github.com/alessandrocaetanob/smooth-operator',
          label: 'GitHub',
          position: 'right',
        },
        {
          href: 'http://localhost:4200',
          label: 'Open App ↗',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            { label: 'Getting Started', to: '/docs/getting-started' },
            { label: 'User Guide', to: '/docs/user-guide' },
            { label: 'Admin Guide', to: '/docs/admin-guide' },
            { label: 'API Reference', to: '/docs/api-reference' },
          ],
        },
        {
          title: 'Application',
          items: [
            { label: 'Open App', href: 'http://localhost:4200' },
            { label: 'Swagger UI', href: 'http://localhost:5000/swagger' },
          ],
        },
        {
          title: 'Project',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/alessandrocaetanob/smooth-operator',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Smooth Operator. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['bash', 'json', 'csharp', 'typescript'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
