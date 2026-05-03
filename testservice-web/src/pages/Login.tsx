import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogIn } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { getErrorMessage } from '../types';

const Login: React.FC = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    const trimmedUsername = username.trim();
    const trimmedPassword = password.trim();
    if (!trimmedUsername || !trimmedPassword) {
      setError('Username or password cannot be empty.');
      setIsLoading(false);
      return;
    }

    try {
      await login(trimmedUsername, trimmedPassword);
      navigate('/');
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-transparent px-4 py-10">
      <div className="mx-auto grid min-h-[calc(100vh-5rem)] max-w-6xl items-center gap-8 lg:grid-cols-[minmax(0,1.15fr)_minmax(380px,460px)]">
        <section className="hidden lg:block">
          <div className="page-hero min-h-[620px]">
            <p className="eyebrow">Enterprise Test Data Platform</p>
            <div className="mt-6 flex items-start gap-4">
              <div className="page-hero-icon">
                <LogIn className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <h1 className="text-4xl font-semibold leading-tight text-white">Control schemas, entities, mocks, and access from one operating surface.</h1>
                <p className="mt-5 max-w-xl text-base leading-7 text-slate-300">
                  Test Service centralizes dynamic test data management for engineering and QA teams with high-visibility operational tooling.
                </p>
              </div>
            </div>

            <div className="mt-10 grid gap-3 md:grid-cols-3">
              <div className="stat-card">
                <p className="text-sm text-slate-400">Data Modeling</p>
                <p className="mt-3 text-lg font-semibold text-white">Schemas + Entities</p>
              </div>
              <div className="stat-card">
                <p className="text-sm text-slate-400">Traffic Control</p>
                <p className="mt-3 text-lg font-semibold text-white">Mocks + Verification</p>
              </div>
              <div className="stat-card">
                <p className="text-sm text-slate-400">Administration</p>
                <p className="mt-3 text-lg font-semibold text-white">Users + API Keys</p>
              </div>
            </div>
          </div>
        </section>

        <div className="panel-strong mx-auto w-full max-w-md p-8">
          <div className="mb-8">
            <div className="inline-flex items-center justify-center rounded-2xl border border-blue-400/25 bg-blue-500/12 p-4">
              <LogIn className="h-7 w-7 text-blue-300" />
            </div>
            <h1 className="mt-5 text-3xl font-semibold text-white">Sign in</h1>
            <p className="mt-2 text-sm leading-6 text-slate-400">Use your admin or contributor credentials to access the workspace.</p>
          </div>

          {error && (
            <div role="alert" className="mb-4 flex items-center gap-2 rounded-2xl border border-red-500/35 bg-red-500/10 p-3 text-red-300">
              <svg className="h-5 w-5 flex-shrink-0 text-red-500" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h8m-4-4v8" />
              </svg>
              <span className="text-sm">{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-6">
            <div>
              <label htmlFor="username" className="mb-2 block text-sm font-medium text-slate-300">
                Username
              </label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                  <svg className="h-5 w-5 text-gray-500" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 11.576V4H8v7.576A4.992 4.992 0 0112 17c1.104 0 2.12-.373 3-1.003M8 21h8M8 17h8" />
                  </svg>
                </div>
                <input
                  id="username"
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  onBlur={(e) => setUsername(e.target.value.trim())}
                  className="field-shell block w-full pl-10 pr-3"
                  placeholder="Enter your username"
                  required
                  autoFocus
                />
              </div>
            </div>

            <div>
              <label htmlFor="password" className="mb-2 block text-sm font-medium text-slate-300">
                Password
              </label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                  <svg className="h-5 w-5 text-gray-500" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 11.576V4H8v7.576A4.992 4.992 0 0112 17c1.104 0 2.12-.373 3-1.003M8 21h8m-8-4h8" />
                  </svg>
                </div>
                <input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  onBlur={(e) => setPassword(e.target.value.trim())}
                  className="field-shell block w-full pl-10 pr-3"
                  placeholder="Enter your password"
                  required
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="button-primary w-full rounded-2xl py-3 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isLoading ? (
                <span className="flex items-center justify-center">
                  <svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  Signing in...
                </span>
              ) : (
                'Sign In'
              )}
            </button>
          </form>

          {import.meta.env.DEV && (
            <div className="mt-6 rounded-2xl border border-slate-700 bg-slate-900/75 p-4">
              <p className="text-center text-xs text-slate-400">
                Default credentials: <span className="text-white font-medium">admin</span> / <span className="text-white font-medium">Admin@123</span>
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default Login;
