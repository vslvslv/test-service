/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // Grafana-inspired colors
        dark: {
          bg: {
            primary: '#0b0c0e',
            secondary: '#141619',
            tertiary: '#1e1e1e',
          },
          surface: {
            primary: '#1e1e1e',
            secondary: '#262626',
            hover: '#2e2e2e',
          },
          border: {
            primary: '#313338',
            secondary: '#404040',
          },
          text: {
            primary: '#e6edf3',
            secondary: '#9198a1',
            tertiary: '#6e7681',
          },
        },
        light: {
          bg: {
            primary: '#ffffff',
            secondary: '#f6f8fa',
            tertiary: '#eaeef2',
          },
          surface: {
            primary: '#ffffff',
            secondary: '#f6f8fa',
            hover: '#eef1f6',
          },
          border: {
            primary: '#d0d7de',
            secondary: '#e1e4e8',
          },
          text: {
            primary: '#1f2328',
            secondary: '#656d76',
            tertiary: '#8c959f',
          },
        },
        primary: {
          DEFAULT: '#3d71ff',
          hover: '#5589ff',
          light: '#e3ebff',
          dark: '#2559e8',
        },
        success: {
          DEFAULT: '#56d364',
          hover: '#6de07c',
          light: '#e3f7e6',
          dark: '#3fb950',
        },
        warning: {
          DEFAULT: '#e3b341',
          hover: '#e8c05a',
          light: '#fff8e3',
          dark: '#bf8700',
        },
        error: {
          DEFAULT: '#f85149',
          hover: '#ff6b62',
          light: '#ffe5e3',
          dark: '#da3633',
        },
        info: {
          DEFAULT: '#4493f8',
          hover: '#5da5ff',
          light: '#e3f0ff',
          dark: '#2f81f7',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
        mono: ['JetBrains Mono', 'Consolas', 'Monaco', 'monospace'],
      },
      fontSize: {
        'xs': ['0.75rem', { lineHeight: '1rem' }],
        'sm': ['0.875rem', { lineHeight: '1.25rem' }],
        'base': ['1rem', { lineHeight: '1.5rem' }],
        'lg': ['1.125rem', { lineHeight: '1.75rem' }],
        'xl': ['1.25rem', { lineHeight: '1.75rem' }],
        '2xl': ['1.5rem', { lineHeight: '2rem' }],
        '3xl': ['1.875rem', { lineHeight: '2.25rem' }],
        '4xl': ['2.25rem', { lineHeight: '2.5rem' }],
      },
      borderRadius: {
        'sm': '0.25rem',
        DEFAULT: '0.375rem',
        'md': '0.5rem',
        'lg': '0.75rem',
        'xl': '1rem',
      },
      boxShadow: {
        'sm': '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
        DEFAULT: '0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px -1px rgba(0, 0, 0, 0.1)',
        'md': '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1)',
        'lg': '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -4px rgba(0, 0, 0, 0.1)',
        'xl': '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1)',
        'dark-sm': '0 1px 2px 0 rgba(0, 0, 0, 0.3)',
        'dark': '0 1px 3px 0 rgba(0, 0, 0, 0.4), 0 1px 2px -1px rgba(0, 0, 0, 0.4)',
        'dark-md': '0 4px 6px -1px rgba(0, 0, 0, 0.4), 0 2px 4px -2px rgba(0, 0, 0, 0.4)',
      },
      animation: {
        'fade-in': 'fadeIn 0.2s ease-in',
        'slide-in': 'slideIn 0.3s ease-out',
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideIn: {
          '0%': { transform: 'translateY(-10px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
      },
    },
  },
  plugins: [],
}
