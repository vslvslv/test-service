import React, { useEffect, useMemo, useState } from 'react';
import { Plus, Pencil, Trash2, RefreshCw, ToggleLeft, ToggleRight, Boxes, X, Copy } from 'lucide-react';
import { apiService } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import type { Environment, MockExpectation, MockRequestLog, MockVerificationRequest, MockVerificationResponse } from '../types';
import { getErrorMessage } from '../types';
import { Permissions } from '../utils/permissions';

type KeyValueRow = { key: string; value: string };
type LogTimeRange = '15m' | '1h' | '6h' | '24h' | '7d';
type VerifyCountMode = 'exact' | 'atLeast' | 'atMost' | 'range';

interface VerifyPreset {
  id: string;
  name: string;
  createdAt: string;
  method: string;
  path: string;
  pathMatchType: number;
  bodyMatchType: number;
  body: string;
  queryRows: KeyValueRow[];
  headerRows: KeyValueRow[];
  countMode: VerifyCountMode;
  exactCount: number;
  minCount: number;
  maxCount: number;
}

type MockFormState = {
  environment: string;
  name: string;
  priority: number;
  enabled: boolean;
  method: string;
  path: string;
  pathMatchType: number;
  body: string;
  bodyMatchType: number;
  status: number;
  responseBody: string;
  delayMs: number;
  unlimited: boolean;
  remaining: number;
  queryRows: KeyValueRow[];
  headerRows: KeyValueRow[];
  responseHeaderRows: KeyValueRow[];
};

const defaultForm = (environment: string): MockFormState => ({
  environment,
  name: '',
  priority: 0,
  enabled: true,
  method: 'GET',
  path: '/',
  pathMatchType: 0,
  body: '',
  bodyMatchType: 0,
  status: 200,
  responseBody: '',
  delayMs: 0,
  unlimited: true,
  remaining: 1,
  queryRows: [],
  headerRows: [],
  responseHeaderRows: [{ key: 'Content-Type', value: 'application/json' }]
});

const pathMatchTypeLabel = (value: number) => {
  switch (value) {
    case 1: return 'Prefix';
    case 2: return 'Regex';
    default: return 'Exact';
  }
};

const bodyMatchTypeLabel = (value: number) => {
  switch (value) {
    case 1: return 'Exact';
    case 2: return 'Contains';
    case 3: return 'Regex';
    default: return 'Any';
  }
};

const toIntEnum = (value: unknown, fallback: number) => {
  if (typeof value === 'number') return value;
  if (typeof value === 'string') {
    const lower = value.toLowerCase();
    if (lower === 'exact') return 0;
    if (lower === 'prefix') return 1;
    if (lower === 'regex') return 2;
    if (lower === 'any') return 0;
    if (lower === 'contains') return 2;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }
  return fallback;
};

const dictionaryToRows = (dictionary?: Record<string, string>): KeyValueRow[] => {
  if (!dictionary) return [];
  return Object.entries(dictionary)
    .map(([key, value]) => ({ key, value: value ?? '' }))
    .filter((row) => row.key.trim() !== '');
};

const rowsToDictionary = (rows: KeyValueRow[]): Record<string, string> =>
  rows
    .filter((row) => row.key.trim().length > 0)
    .reduce<Record<string, string>>((acc, row) => {
      acc[row.key.trim()] = row.value ?? '';
      return acc;
    }, {});

const normalizeExpectation = (exp: MockExpectation): MockExpectation => ({
  ...exp,
  requestMatcher: {
    ...exp.requestMatcher,
    pathMatchType: toIntEnum(exp.requestMatcher?.pathMatchType as unknown, 0) as unknown as never,
    bodyMatchType: toIntEnum(exp.requestMatcher?.bodyMatchType as unknown, 0) as unknown as never,
    query: exp.requestMatcher?.query || {},
    headers: exp.requestMatcher?.headers || {}
  },
  responseTemplate: {
    ...exp.responseTemplate,
    headers: exp.responseTemplate?.headers || {},
    status: exp.responseTemplate?.status || 200,
    delayMs: exp.responseTemplate?.delayMs || 0
  },
  times: {
    unlimited: exp.times?.unlimited ?? true,
    remaining: exp.times?.remaining ?? 0
  }
});

const formatTimestamp = (value?: string) => {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
};

const logRangeMeta: Record<LogTimeRange, { label: string; durationMs: number; buckets: number }> = {
  '15m': { label: 'Last 15m', durationMs: 15 * 60 * 1000, buckets: 15 },
  '1h': { label: 'Last 1h', durationMs: 60 * 60 * 1000, buckets: 30 },
  '6h': { label: 'Last 6h', durationMs: 6 * 60 * 60 * 1000, buckets: 36 },
  '24h': { label: 'Last 24h', durationMs: 24 * 60 * 60 * 1000, buckets: 48 },
  '7d': { label: 'Last 7d', durationMs: 7 * 24 * 60 * 60 * 1000, buckets: 56 }
};

const formatTimeAxisLabel = (timestampMs: number, range: LogTimeRange) => {
  const date = new Date(timestampMs);
  if (range === '7d') {
    return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
  return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
};

const verifyPresetStorageKey = 'mocks.verify.presets.v1';

const methodBadgeClass = (method?: string) => {
  switch ((method || 'ANY').toUpperCase()) {
    case 'GET':
      return 'bg-emerald-500/15 border-emerald-500/30 text-emerald-300';
    case 'POST':
      return 'bg-blue-500/15 border-blue-500/30 text-blue-300';
    case 'PUT':
    case 'PATCH':
      return 'bg-amber-500/15 border-amber-500/30 text-amber-300';
    case 'DELETE':
      return 'bg-red-500/15 border-red-500/30 text-red-300';
    default:
      return 'bg-gray-600/30 border-gray-500/30 text-gray-300';
  }
};

const Mocks: React.FC = () => {
  const { hasPermission } = useAuth();
  const canWriteMocks = hasPermission(Permissions.MocksWrite);
  const canReadLogs = hasPermission(Permissions.MocksLogsRead);
  const canDeleteLogs = hasPermission(Permissions.MocksLogsDelete);
  const canVerifyMocks = hasPermission(Permissions.MocksVerify);
  const [activeTab, setActiveTab] = useState<'expectations' | 'logs' | 'verify'>('expectations');
  const [environment, setEnvironment] = useState('dev');
  const [includeDisabled, setIncludeDisabled] = useState(true);
  const [availableEnvironments, setAvailableEnvironments] = useState<Environment[]>([]);
  const [expectations, setExpectations] = useState<MockExpectation[]>([]);
  const [logs, setLogs] = useState<MockRequestLog[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingLogs, setIsLoadingLogs] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isClearingLogs, setIsClearingLogs] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<MockFormState>(defaultForm('dev'));
  const [logsPathInput, setLogsPathInput] = useState('');
  const [logsMatchedInput, setLogsMatchedInput] = useState<'all' | 'matched' | 'unmatched'>('all');
  const [logsLimitInput, setLogsLimitInput] = useState(100);
  const [logsPathFilter, setLogsPathFilter] = useState('');
  const [logsMatchedFilter, setLogsMatchedFilter] = useState<'all' | 'matched' | 'unmatched'>('all');
  const [logsLimit, setLogsLimit] = useState(100);
  const [logsTimeRange, setLogsTimeRange] = useState<LogTimeRange>('1h');
  const [showTotalSeries, setShowTotalSeries] = useState(true);
  const [showMatchedSeries, setShowMatchedSeries] = useState(true);
  const [showUnmatchedSeries, setShowUnmatchedSeries] = useState(true);
  const [selectedLog, setSelectedLog] = useState<MockRequestLog | null>(null);
  const [verifyMethod, setVerifyMethod] = useState('ANY');
  const [verifyPath, setVerifyPath] = useState('/');
  const [verifyPathMatchType, setVerifyPathMatchType] = useState(0);
  const [verifyBodyMatchType, setVerifyBodyMatchType] = useState(0);
  const [verifyBody, setVerifyBody] = useState('');
  const [verifyQueryRows, setVerifyQueryRows] = useState<KeyValueRow[]>([]);
  const [verifyHeaderRows, setVerifyHeaderRows] = useState<KeyValueRow[]>([]);
  const [verifyCountMode, setVerifyCountMode] = useState<VerifyCountMode>('exact');
  const [verifyExactCount, setVerifyExactCount] = useState(1);
  const [verifyMinCount, setVerifyMinCount] = useState(1);
  const [verifyMaxCount, setVerifyMaxCount] = useState(1);
  const [isVerifying, setIsVerifying] = useState(false);
  const [verifyResult, setVerifyResult] = useState<MockVerificationResponse | null>(null);
  const [verifyPresetName, setVerifyPresetName] = useState('');
  const [verifyPresets, setVerifyPresets] = useState<VerifyPreset[]>([]);

  useEffect(() => {
    let cancelled = false;
    const loadEnvironments = async () => {
      try {
        const data = await apiService.getEnvironments();
        if (cancelled) return;
        setAvailableEnvironments(data || []);
        if ((!environment || !environment.trim()) && (data || []).length > 0) {
          const firstEnv = (data || [])[0];
          if (firstEnv?.name) {
            setEnvironment(firstEnv.name);
            setForm((prev) => ({ ...prev, environment: firstEnv.name }));
          }
        }
      } catch {
        if (!cancelled) setAvailableEnvironments([]);
      }
    };
    loadEnvironments();
    return () => {
      cancelled = true;
    };
  }, []);

  const loadExpectations = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await apiService.getMockExpectations(environment, includeDisabled);
      setExpectations((data || []).map(normalizeExpectation));
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadExpectations();
  }, [environment, includeDisabled]);

  const loadRequestLogs = async () => {
    setIsLoadingLogs(true);
    setError('');
    try {
      const matched =
        logsMatchedFilter === 'all'
          ? undefined
          : logsMatchedFilter === 'matched';
      const data = await apiService.getMockRequestLogs(
        environment,
        logsPathFilter.trim() || undefined,
        logsLimit,
        matched
      );
      setLogs(data || []);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsLoadingLogs(false);
    }
  };

  useEffect(() => {
    if (activeTab !== 'logs' || !canReadLogs) return;
    loadRequestLogs();
  }, [activeTab, environment, logsPathFilter, logsMatchedFilter, logsLimit, canReadLogs]);

  useEffect(() => {
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      if (selectedLog) {
        setSelectedLog(null);
        return;
      }
      if (showModal) {
        closeModal();
      }
    };
    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  }, [showModal, selectedLog]);

  useEffect(() => {
    try {
      const raw = localStorage.getItem(verifyPresetStorageKey);
      if (!raw) return;
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed)) {
        setVerifyPresets(parsed);
      }
    } catch {
      setVerifyPresets([]);
    }
  }, []);

  useEffect(() => {
    try {
      localStorage.setItem(verifyPresetStorageKey, JSON.stringify(verifyPresets));
    } catch {
      // Ignore storage quota/errors for now.
    }
  }, [verifyPresets]);

  useEffect(() => {
    if (activeTab !== 'logs') {
      setSelectedLog(null);
      return;
    }
    if (selectedLog && !logs.some((item) => item.id === selectedLog.id)) {
      setSelectedLog(null);
    }
  }, [activeTab, logs, selectedLog]);

  const sortedExpectations = useMemo(() => {
    return [...expectations].sort((a, b) => {
      if (a.priority !== b.priority) return b.priority - a.priority;
      return (a.name || '').localeCompare(b.name || '');
    });
  }, [expectations]);

  const logChart = useMemo(() => {
    const now = Date.now();
    const range = logRangeMeta[logsTimeRange];
    const start = now - range.durationMs;
    const bucketSize = range.durationMs / range.buckets;
    const buckets = Array.from({ length: range.buckets }, (_, index) => ({
      index,
      start: start + index * bucketSize,
      total: 0,
      matched: 0,
      unmatched: 0
    }));

    for (const log of logs) {
      if (!log.timestamp) continue;
      const ts = new Date(log.timestamp).getTime();
      if (Number.isNaN(ts) || ts < start || ts > now) continue;
      const idx = Math.min(range.buckets - 1, Math.floor((ts - start) / bucketSize));
      const bucket = buckets[idx];
      bucket.total += 1;
      if (log.matched) {
        bucket.matched += 1;
      } else {
        bucket.unmatched += 1;
      }
    }

    const totalInRange = buckets.reduce((acc, bucket) => acc + bucket.total, 0);
    const matchedInRange = buckets.reduce((acc, bucket) => acc + bucket.matched, 0);
    const unmatchedInRange = buckets.reduce((acc, bucket) => acc + bucket.unmatched, 0);
    const maxValue = Math.max(1, ...buckets.map((bucket) => bucket.total));

    return {
      now,
      start,
      range,
      buckets,
      totalInRange,
      matchedInRange,
      unmatchedInRange,
      maxValue
    };
  }, [logs, logsTimeRange]);

  const environmentOptions = useMemo(() => {
    const fromDb = availableEnvironments
      .map((env) => env.name)
      .filter((name): name is string => !!name && name.trim().length > 0);
    const extras = [environment, form.environment]
      .filter((name): name is string => !!name && name.trim().length > 0);
    return Array.from(new Set([...fromDb, ...extras]));
  }, [availableEnvironments, environment, form.environment]);

  const mockStats = useMemo(() => {
    const totalExpectations = expectations.length;
    const enabledExpectations = expectations.filter((item) => item.enabled).length;
    const disabledExpectations = totalExpectations - enabledExpectations;
    const totalLogs = logs.length;
    const matchedLogs = logs.filter((item) => item.matched).length;
    const unmatchedLogs = totalLogs - matchedLogs;
    const matchedRate = totalLogs > 0 ? Math.round((matchedLogs / totalLogs) * 100) : 0;

    return {
      totalExpectations,
      enabledExpectations,
      disabledExpectations,
      totalLogs,
      matchedLogs,
      unmatchedLogs,
      matchedRate
    };
  }, [expectations, logs]);

  const verifyAssertionSummary = useMemo(() => {
    if (verifyCountMode === 'exact') return `Expect exactly ${verifyExactCount} matching requests`;
    if (verifyCountMode === 'atLeast') return `Expect at least ${verifyMinCount} matching requests`;
    if (verifyCountMode === 'atMost') return `Expect at most ${verifyMaxCount} matching requests`;
    return `Expect between ${verifyMinCount} and ${verifyMaxCount} matching requests`;
  }, [verifyCountMode, verifyExactCount, verifyMinCount, verifyMaxCount]);

  const resetForm = () => {
    setEditingId(null);
    setForm(defaultForm(environment));
  };

  const openCreate = () => {
    resetForm();
    setShowModal(true);
  };

  const openEdit = (expectation: MockExpectation) => {
    setEditingId(expectation.id || null);
    setForm({
      environment: expectation.environment || environment,
      name: expectation.name || '',
      priority: expectation.priority || 0,
      enabled: expectation.enabled ?? true,
      method: expectation.requestMatcher?.method || 'GET',
      path: expectation.requestMatcher?.path || '/',
      pathMatchType: toIntEnum(expectation.requestMatcher?.pathMatchType as unknown, 0),
      body: expectation.requestMatcher?.body || '',
      bodyMatchType: toIntEnum(expectation.requestMatcher?.bodyMatchType as unknown, 0),
      status: expectation.responseTemplate?.status || 200,
      responseBody: expectation.responseTemplate?.body || '',
      delayMs: expectation.responseTemplate?.delayMs || 0,
      unlimited: expectation.times?.unlimited ?? true,
      remaining: expectation.times?.remaining ?? 1,
      queryRows: dictionaryToRows(expectation.requestMatcher?.query),
      headerRows: dictionaryToRows(expectation.requestMatcher?.headers),
      responseHeaderRows: dictionaryToRows(expectation.responseTemplate?.headers)
    });
    setShowModal(true);
  };

  const closeModal = () => {
    setShowModal(false);
    resetForm();
  };

  const setField = <K extends keyof MockFormState>(key: K, value: MockFormState[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const setRow = (type: 'queryRows' | 'headerRows' | 'responseHeaderRows', index: number, key: 'key' | 'value', value: string) => {
    setForm((prev) => {
      const copy = [...prev[type]];
      copy[index] = { ...copy[index], [key]: value };
      return { ...prev, [type]: copy };
    });
  };

  const addRow = (type: 'queryRows' | 'headerRows' | 'responseHeaderRows') => {
    setForm((prev) => ({ ...prev, [type]: [...prev[type], { key: '', value: '' }] }));
  };

  const removeRow = (type: 'queryRows' | 'headerRows' | 'responseHeaderRows', index: number) => {
    setForm((prev) => ({ ...prev, [type]: prev[type].filter((_, i) => i !== index) }));
  };

  const buildPayload = (): MockExpectation => ({
    environment: form.environment.trim() || environment,
    name: form.name.trim(),
    priority: Number(form.priority) || 0,
    enabled: form.enabled,
    requestMatcher: {
      method: form.method.toUpperCase(),
      path: form.path.trim() || '/',
      pathMatchType: form.pathMatchType as unknown as never,
      query: rowsToDictionary(form.queryRows),
      headers: rowsToDictionary(form.headerRows),
      body: form.body.trim() ? form.body : undefined,
      bodyMatchType: form.bodyMatchType as unknown as never
    },
    responseTemplate: {
      status: Number(form.status) || 200,
      headers: rowsToDictionary(form.responseHeaderRows),
      body: form.responseBody,
      delayMs: Math.max(0, Number(form.delayMs) || 0)
    },
    times: {
      unlimited: form.unlimited,
      remaining: form.unlimited ? 0 : Math.max(1, Number(form.remaining) || 1)
    }
  });

  const validateForm = (): string | null => {
    if (!form.name.trim()) return 'Expectation name is required.';
    if (!form.environment.trim()) return 'Environment is required.';
    if (!form.path.trim()) return 'Request path is required.';
    if (!form.unlimited && (!Number.isFinite(Number(form.remaining)) || Number(form.remaining) < 1)) {
      return 'Remaining calls must be at least 1 when unlimited is off.';
    }
    if (!Number.isFinite(Number(form.status)) || Number(form.status) < 100 || Number(form.status) > 599) {
      return 'Response status must be a valid HTTP status code (100-599).';
    }
    return null;
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    const validationError = validateForm();
    if (validationError) {
      setError(validationError);
      return;
    }

    setIsSaving(true);
    try {
      const payload = buildPayload();
      if (editingId) {
        await apiService.updateMockExpectation(editingId, payload);
        setSuccess(`Updated expectation "${payload.name}".`);
      } else {
        await apiService.createMockExpectation(payload);
        setSuccess(`Created expectation "${payload.name}".`);
      }
      closeModal();
      await loadExpectations();
      setTimeout(() => setSuccess(''), 3500);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async (expectation: MockExpectation) => {
    if (!expectation.id) return;
    if (!confirm(`Delete mock expectation "${expectation.name}"?`)) return;
    try {
      await apiService.deleteMockExpectation(expectation.id);
      setSuccess(`Deleted expectation "${expectation.name}".`);
      setTimeout(() => setSuccess(''), 3500);
      await loadExpectations();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  };

  const toggleEnabled = async (expectation: MockExpectation) => {
    if (!expectation.id) return;
    try {
      await apiService.updateMockExpectation(expectation.id, {
        ...expectation,
        enabled: !expectation.enabled
      });
      await loadExpectations();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  };

  const handleClone = (expectation: MockExpectation) => {
    if (!canWriteMocks) return;
    setEditingId(null);
    setForm({
      environment: expectation.environment || environment,
      name: `${expectation.name || 'Expectation'} (Copy)`,
      priority: expectation.priority || 0,
      enabled: expectation.enabled ?? true,
      method: expectation.requestMatcher?.method || 'GET',
      path: expectation.requestMatcher?.path || '/',
      pathMatchType: toIntEnum(expectation.requestMatcher?.pathMatchType as unknown, 0),
      body: expectation.requestMatcher?.body || '',
      bodyMatchType: toIntEnum(expectation.requestMatcher?.bodyMatchType as unknown, 0),
      status: expectation.responseTemplate?.status || 200,
      responseBody: expectation.responseTemplate?.body || '',
      delayMs: expectation.responseTemplate?.delayMs || 0,
      unlimited: expectation.times?.unlimited ?? true,
      remaining: expectation.times?.remaining ?? 1,
      queryRows: dictionaryToRows(expectation.requestMatcher?.query),
      headerRows: dictionaryToRows(expectation.requestMatcher?.headers),
      responseHeaderRows: dictionaryToRows(expectation.responseTemplate?.headers)
    });
    setShowModal(true);
  };

  const handleRefresh = async () => {
    if (activeTab === 'logs') {
      if (!canReadLogs) return;
      await loadRequestLogs();
      return;
    }
    await loadExpectations();
  };

  const handleClearLogs = async () => {
    if (!canDeleteLogs) return;
    if (!confirm(`Delete request logs for environment "${environment}"?`)) return;
    setIsClearingLogs(true);
    setError('');
    try {
      const result = await apiService.deleteMockRequestLogs(environment);
      setSuccess(`Deleted ${result.deletedCount ?? 0} request logs.`);
      setTimeout(() => setSuccess(''), 3500);
      await loadRequestLogs();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsClearingLogs(false);
    }
  };

  const applyLogsFilters = () => {
    setLogsPathFilter(logsPathInput);
    setLogsMatchedFilter(logsMatchedInput);
    setLogsLimit(logsLimitInput);
  };

  const resetLogsFilters = () => {
    setLogsPathInput('');
    setLogsMatchedInput('all');
    setLogsLimitInput(100);
    setLogsPathFilter('');
    setLogsMatchedFilter('all');
    setLogsLimit(100);
  };

  const saveVerifyPreset = () => {
    const trimmedName = verifyPresetName.trim();
    if (!trimmedName) {
      setError('Preset name is required.');
      return;
    }
    const preset: VerifyPreset = {
      id: `${Date.now()}`,
      name: trimmedName,
      createdAt: new Date().toISOString(),
      method: verifyMethod,
      path: verifyPath,
      pathMatchType: verifyPathMatchType,
      bodyMatchType: verifyBodyMatchType,
      body: verifyBody,
      queryRows: verifyQueryRows,
      headerRows: verifyHeaderRows,
      countMode: verifyCountMode,
      exactCount: verifyExactCount,
      minCount: verifyMinCount,
      maxCount: verifyMaxCount
    };
    setVerifyPresets((prev) => [preset, ...prev].slice(0, 20));
    setVerifyPresetName('');
    setSuccess(`Saved preset "${trimmedName}".`);
    setTimeout(() => setSuccess(''), 2500);
  };

  const loadVerifyPreset = (preset: VerifyPreset) => {
    setVerifyMethod(preset.method);
    setVerifyPath(preset.path);
    setVerifyPathMatchType(preset.pathMatchType);
    setVerifyBodyMatchType(preset.bodyMatchType);
    setVerifyBody(preset.body);
    setVerifyQueryRows(preset.queryRows || []);
    setVerifyHeaderRows(preset.headerRows || []);
    setVerifyCountMode(preset.countMode);
    setVerifyExactCount(preset.exactCount);
    setVerifyMinCount(preset.minCount);
    setVerifyMaxCount(preset.maxCount);
    setSuccess(`Loaded preset "${preset.name}".`);
    setTimeout(() => setSuccess(''), 2500);
  };

  const deleteVerifyPreset = (presetId: string) => {
    setVerifyPresets((prev) => prev.filter((item) => item.id !== presetId));
  };

  const jumpToLogsFromVerify = () => {
    setLogsPathInput(verifyPath);
    setLogsPathFilter(verifyPath);
    setActiveTab('logs');
  };

  const setVerifyRow = (type: 'query' | 'header', index: number, key: 'key' | 'value', value: string) => {
    if (type === 'query') {
      setVerifyQueryRows((prev) => {
        const copy = [...prev];
        copy[index] = { ...copy[index], [key]: value };
        return copy;
      });
      return;
    }
    setVerifyHeaderRows((prev) => {
      const copy = [...prev];
      copy[index] = { ...copy[index], [key]: value };
      return copy;
    });
  };

  const addVerifyRow = (type: 'query' | 'header') => {
    if (type === 'query') {
      setVerifyQueryRows((prev) => [...prev, { key: '', value: '' }]);
      return;
    }
    setVerifyHeaderRows((prev) => [...prev, { key: '', value: '' }]);
  };

  const removeVerifyRow = (type: 'query' | 'header', index: number) => {
    if (type === 'query') {
      setVerifyQueryRows((prev) => prev.filter((_, i) => i !== index));
      return;
    }
    setVerifyHeaderRows((prev) => prev.filter((_, i) => i !== index));
  };

  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canVerifyMocks) return;
    setError('');
    setSuccess('');
    setVerifyResult(null);

    if (!verifyPath.trim()) {
      setError('Verification path is required.');
      return;
    }

    if (verifyCountMode === 'exact' && verifyExactCount < 0) {
      setError('Exact count must be 0 or greater.');
      return;
    }
    if (verifyCountMode === 'atLeast' && verifyMinCount < 0) {
      setError('Minimum count must be 0 or greater.');
      return;
    }
    if (verifyCountMode === 'atMost' && verifyMaxCount < 0) {
      setError('Maximum count must be 0 or greater.');
      return;
    }
    if (verifyCountMode === 'range' && (verifyMinCount < 0 || verifyMaxCount < 0 || verifyMinCount > verifyMaxCount)) {
      setError('Range values are invalid. Ensure min <= max and both are >= 0.');
      return;
    }

    const payload: MockVerificationRequest = {
      environment: environment.trim() || 'dev',
      matcher: {
        method: verifyMethod === 'ANY' ? undefined : verifyMethod,
        path: verifyPath.trim(),
        pathMatchType: verifyPathMatchType as unknown as never,
        query: rowsToDictionary(verifyQueryRows),
        headers: rowsToDictionary(verifyHeaderRows),
        body: verifyBody.trim() ? verifyBody : undefined,
        bodyMatchType: verifyBodyMatchType as unknown as never
      }
    };

    if (verifyCountMode === 'exact') payload.exactCount = verifyExactCount;
    if (verifyCountMode === 'atLeast') payload.minCount = verifyMinCount;
    if (verifyCountMode === 'atMost') payload.maxCount = verifyMaxCount;
    if (verifyCountMode === 'range') {
      payload.minCount = verifyMinCount;
      payload.maxCount = verifyMaxCount;
    }

    setIsVerifying(true);
    try {
      const result = await apiService.verifyMockRequests(payload);
      setVerifyResult(result);
      if (result.success) {
        setSuccess(`Verification passed (${result.matchedCount} matches).`);
      } else {
        setError(result.message || `Verification failed (${result.matchedCount} matches).`);
      }
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsVerifying(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="rounded-xl border border-gray-700 bg-gradient-to-br from-gray-800 to-gray-900 p-5 space-y-5">
        <div className="space-y-4">
          <div className="min-w-0">
            <h1 className="text-2xl font-bold text-white flex items-center gap-2">
              <Boxes className="w-7 h-7 text-cyan-400 shrink-0" />
              Mocks
            </h1>
            <p className="text-gray-400 mt-1">Manage expectations, inspect traffic, and verify behavior per environment.</p>
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <label className="text-sm text-gray-300">
              Environment
              <select
                value={environment}
                onChange={(e) => setEnvironment(e.target.value)}
                className="mt-1 block min-w-40 px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                disabled={environmentOptions.length === 0}
              >
                {environmentOptions.length === 0 ? (
                  <option value="">No environments</option>
                ) : (
                  environmentOptions.map((envName) => (
                    <option key={envName} value={envName}>{envName}</option>
                  ))
                )}
              </select>
            </label>
            {activeTab === 'expectations' && (
              <label className="text-sm text-gray-300 flex items-center gap-2 mb-2">
                <input
                  type="checkbox"
                  checked={includeDisabled}
                  onChange={(e) => setIncludeDisabled(e.target.checked)}
                />
                Include disabled
              </label>
            )}
            <button
              onClick={handleRefresh}
              className="inline-flex items-center justify-center gap-2 px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded text-white"
            >
              <RefreshCw className="w-4 h-4" />
              Refresh
            </button>
            {activeTab === 'expectations' && canWriteMocks && (
              <button
                onClick={openCreate}
                className="inline-flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded text-white"
              >
                <Plus className="w-4 h-4" />
                Create Expectation
              </button>
            )}
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3">
          <div className="rounded-lg border border-gray-700 bg-gray-800/70 px-4 py-3">
            <p className="text-xs uppercase tracking-wider text-gray-500">Expectations</p>
            <p className="text-xl font-semibold text-white mt-1">{mockStats.totalExpectations}</p>
            <p className="text-xs text-gray-400 mt-1">
              <span className="text-green-300">{mockStats.enabledExpectations} enabled</span> · <span className="text-gray-300">{mockStats.disabledExpectations} disabled</span>
            </p>
          </div>
          <div className="rounded-lg border border-gray-700 bg-gray-800/70 px-4 py-3">
            <p className="text-xs uppercase tracking-wider text-gray-500">Traffic Sample</p>
            <p className="text-xl font-semibold text-white mt-1">{mockStats.totalLogs}</p>
            <p className="text-xs text-gray-400 mt-1">Current fetched logs set</p>
          </div>
          <div className="rounded-lg border border-gray-700 bg-gray-800/70 px-4 py-3">
            <p className="text-xs uppercase tracking-wider text-gray-500">Matched Rate</p>
            <p className="text-xl font-semibold text-cyan-300 mt-1">{mockStats.matchedRate}%</p>
            <p className="text-xs text-gray-400 mt-1">{mockStats.matchedLogs} matched / {mockStats.unmatchedLogs} unmatched</p>
          </div>
          <div className="rounded-lg border border-gray-700 bg-gray-800/70 px-4 py-3">
            <p className="text-xs uppercase tracking-wider text-gray-500">Active Context</p>
            <p className="text-xl font-semibold text-white mt-1">{environment || '-'}</p>
            <p className="text-xs text-gray-400 mt-1">
              {activeTab === 'logs' ? `Range: ${logRangeMeta[logsTimeRange].label}` : `Tab: ${activeTab}`}
            </p>
          </div>
        </div>
      </div>

      <div className="flex items-center gap-2 border-b border-gray-700">
        <button
          onClick={() => setActiveTab('expectations')}
          className={`px-4 py-2 text-sm font-medium rounded-t-md border-b-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400/70 ${activeTab === 'expectations' ? 'border-cyan-400 text-white bg-gray-800/60' : 'border-transparent text-gray-400 hover:text-white hover:bg-gray-800/40'}`}
        >
          Expectations
        </button>
        <button
          onClick={() => setActiveTab('logs')}
          className={`px-4 py-2 text-sm font-medium rounded-t-md border-b-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400/70 ${activeTab === 'logs' ? 'border-cyan-400 text-white bg-gray-800/60' : 'border-transparent text-gray-400 hover:text-white hover:bg-gray-800/40'}`}
        >
          Request Logs
        </button>
        <button
          onClick={() => setActiveTab('verify')}
          className={`px-4 py-2 text-sm font-medium rounded-t-md border-b-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400/70 ${activeTab === 'verify' ? 'border-cyan-400 text-white bg-gray-800/60' : 'border-transparent text-gray-400 hover:text-white hover:bg-gray-800/40'}`}
        >
          Verify
        </button>
      </div>

      {error && <div role="alert" aria-live="assertive" className="p-3 rounded border border-red-500/40 bg-red-500/10 text-red-300">{error}</div>}
      {success && <div role="status" aria-live="polite" className="p-3 rounded border border-green-500/40 bg-green-500/10 text-green-300">{success}</div>}

      {activeTab === 'verify' && (
        <div className="space-y-4">
          {!canVerifyMocks ? (
            <div className="p-6 rounded-lg border border-amber-500/40 bg-amber-500/10 text-amber-200">
              You do not have permission to verify mock requests.
            </div>
          ) : (
            <form onSubmit={handleVerify} className="bg-gray-800 border border-gray-700 rounded-lg p-4 space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-white text-lg font-semibold">Verification Flow</h2>
                <p className="text-xs text-gray-400">Guided 3-step verification</p>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-3">
                <h3 className="text-sm font-medium text-cyan-300">Preset Scaffold</h3>
                <p className="text-xs text-gray-400">Save and reuse matcher + assertion combinations.</p>
                <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
                  <label className="text-sm text-gray-300 md:col-span-3">
                    Preset Name
                    <input
                      type="text"
                      value={verifyPresetName}
                      onChange={(e) => setVerifyPresetName(e.target.value)}
                      placeholder="e.g. Orders POST should be called once"
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                    />
                  </label>
                  <div className="md:col-span-1 flex items-end">
                    <button
                      type="button"
                      onClick={saveVerifyPreset}
                      className="w-full px-3 py-2 bg-gray-700 hover:bg-gray-600 rounded text-white text-sm"
                    >
                      Save Preset
                    </button>
                  </div>
                </div>
                <div className="space-y-2">
                  {verifyPresets.length === 0 ? (
                    <p className="text-xs text-gray-500">No presets saved yet.</p>
                  ) : (
                    verifyPresets.map((preset) => (
                      <div key={preset.id} className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between rounded border border-gray-700 bg-gray-800 px-3 py-2">
                        <div>
                          <p className="text-sm text-gray-100">{preset.name}</p>
                          <p className="text-xs text-gray-400">{preset.method} {preset.path} · {preset.countMode}</p>
                        </div>
                        <div className="flex items-center gap-2">
                          <button type="button" onClick={() => loadVerifyPreset(preset)} className="px-2 py-1 text-xs rounded bg-gray-700 hover:bg-gray-600 text-white">Load</button>
                          <button type="button" onClick={() => deleteVerifyPreset(preset.id)} className="px-2 py-1 text-xs rounded bg-red-500/15 hover:bg-red-500/25 text-red-300">Delete</button>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-4">
                <h3 className="text-sm font-medium text-cyan-300">Step 1: Matcher</h3>
                <p className="text-xs text-gray-400">Define the request shape to verify.</p>
              <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <label className="text-sm text-gray-300">
                  Method
                  <select
                    value={verifyMethod}
                    onChange={(e) => setVerifyMethod(e.target.value)}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                  >
                    {['ANY', 'GET', 'POST', 'PUT', 'PATCH', 'DELETE'].map((method) => <option key={method} value={method}>{method}</option>)}
                  </select>
                </label>
                <label className="text-sm text-gray-300 md:col-span-2">
                  Path
                  <input
                    type="text"
                    value={verifyPath}
                    onChange={(e) => setVerifyPath(e.target.value)}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white font-mono"
                    required
                  />
                </label>
                <label className="text-sm text-gray-300">
                  Path Match
                  <select
                    value={verifyPathMatchType}
                    onChange={(e) => setVerifyPathMatchType(Number(e.target.value))}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                  >
                    <option value={0}>Exact</option>
                    <option value={1}>Prefix</option>
                    <option value={2}>Regex</option>
                  </select>
                </label>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <p className="text-xs uppercase tracking-wider text-gray-500">Query Params</p>
                    <button type="button" onClick={() => addVerifyRow('query')} className="text-xs text-blue-300 hover:text-blue-200">+ Add</button>
                  </div>
                  <div className="space-y-2">
                    {verifyQueryRows.map((row, index) => (
                      <div key={`vq-${index}`} className="grid grid-cols-[1fr_1fr_auto] gap-2">
                        <input value={row.key} onChange={(e) => setVerifyRow('query', index, 'key', e.target.value)} placeholder="key" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                        <input value={row.value} onChange={(e) => setVerifyRow('query', index, 'value', e.target.value)} placeholder="value" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                        <button type="button" onClick={() => removeVerifyRow('query', index)} className="px-2 text-red-300 hover:text-red-200">x</button>
                      </div>
                    ))}
                  </div>
                </div>
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <p className="text-xs uppercase tracking-wider text-gray-500">Headers</p>
                    <button type="button" onClick={() => addVerifyRow('header')} className="text-xs text-blue-300 hover:text-blue-200">+ Add</button>
                  </div>
                  <div className="space-y-2">
                    {verifyHeaderRows.map((row, index) => (
                      <div key={`vh-${index}`} className="grid grid-cols-[1fr_1fr_auto] gap-2">
                        <input value={row.key} onChange={(e) => setVerifyRow('header', index, 'key', e.target.value)} placeholder="key" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                        <input value={row.value} onChange={(e) => setVerifyRow('header', index, 'value', e.target.value)} placeholder="value" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                        <button type="button" onClick={() => removeVerifyRow('header', index)} className="px-2 text-red-300 hover:text-red-200">x</button>
                      </div>
                    ))}
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <label className="text-sm text-gray-300">
                  Body Match
                  <select
                    value={verifyBodyMatchType}
                    onChange={(e) => setVerifyBodyMatchType(Number(e.target.value))}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                  >
                    <option value={0}>Any</option>
                    <option value={1}>Exact</option>
                    <option value={2}>Contains</option>
                    <option value={3}>Regex</option>
                  </select>
                </label>
                <label className="text-sm text-gray-300 md:col-span-2">
                  Body Pattern
                  <textarea
                    value={verifyBody}
                    onChange={(e) => setVerifyBody(e.target.value)}
                    rows={3}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white font-mono"
                  />
                </label>
              </div>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-3">
                <h3 className="text-sm font-medium text-cyan-300">Step 2: Count Assertion</h3>
                <p className="text-xs text-gray-400">{verifyAssertionSummary}</p>
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <label className="text-sm text-gray-300 md:col-span-2">
                    Mode
                    <select
                      value={verifyCountMode}
                      onChange={(e) => setVerifyCountMode(e.target.value as VerifyCountMode)}
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                    >
                      <option value="exact">Exact count</option>
                      <option value="atLeast">At least</option>
                      <option value="atMost">At most</option>
                      <option value="range">Range</option>
                    </select>
                  </label>
                  {verifyCountMode === 'exact' && (
                    <label className="text-sm text-gray-300">
                      Exact
                      <input type="number" min={0} value={verifyExactCount} onChange={(e) => setVerifyExactCount(Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                    </label>
                  )}
                  {verifyCountMode === 'atLeast' && (
                    <label className="text-sm text-gray-300">
                      Min
                      <input type="number" min={0} value={verifyMinCount} onChange={(e) => setVerifyMinCount(Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                    </label>
                  )}
                  {verifyCountMode === 'atMost' && (
                    <label className="text-sm text-gray-300">
                      Max
                      <input type="number" min={0} value={verifyMaxCount} onChange={(e) => setVerifyMaxCount(Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                    </label>
                  )}
                  {verifyCountMode === 'range' && (
                    <>
                      <label className="text-sm text-gray-300">
                        Min
                        <input type="number" min={0} value={verifyMinCount} onChange={(e) => setVerifyMinCount(Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                      </label>
                      <label className="text-sm text-gray-300">
                        Max
                        <input type="number" min={0} value={verifyMaxCount} onChange={(e) => setVerifyMaxCount(Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                      </label>
                    </>
                  )}
                </div>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-3">
                <h3 className="text-sm font-medium text-cyan-300">Step 3: Run + Inspect</h3>
                <div className="flex flex-wrap gap-2">
                  <button
                    type="submit"
                    disabled={isVerifying}
                    className="px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded text-white disabled:opacity-50"
                  >
                    {isVerifying ? 'Verifying...' : 'Run Verification'}
                  </button>
                  <button
                    type="button"
                    onClick={jumpToLogsFromVerify}
                    className="px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded text-white"
                  >
                    Open Logs With Path
                  </button>
                </div>
              </div>

              {verifyResult && (
                <div className={`rounded-lg border p-4 ${verifyResult.success ? 'border-green-500/40 bg-green-500/10 text-green-200' : 'border-red-500/40 bg-red-500/10 text-red-200'}`}>
                  <div className="flex items-center justify-between">
                    <div className="font-semibold">{verifyResult.success ? 'Verification Passed' : 'Verification Failed'}</div>
                    <span className="text-xs px-2 py-1 rounded border border-white/20">{verifyAssertionSummary}</span>
                  </div>
                  <div className="text-sm mt-2">Matched requests: {verifyResult.matchedCount}</div>
                  <div className="text-sm mt-1">{verifyResult.message}</div>
                  <div className="mt-3 flex gap-2">
                    <button type="button" onClick={jumpToLogsFromVerify} className="px-3 py-1.5 rounded bg-gray-700 hover:bg-gray-600 text-white text-xs">
                      Investigate In Logs
                    </button>
                  </div>
                </div>
              )}
            </form>
          )}
        </div>
      )}

      {activeTab === 'expectations' && (
        <div className="space-y-3">
          {isLoading && (
            <div className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-8 text-center text-gray-400">
              Loading expectations...
            </div>
          )}
          {!isLoading && sortedExpectations.length === 0 && (
            <div className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-8 text-center text-gray-400">
              No expectations found for this environment.
            </div>
          )}
          {!isLoading && sortedExpectations.map((expectation) => (
            <div key={expectation.id} className="bg-gray-800 border border-gray-700 rounded-lg p-4 hover:border-gray-600 transition-colors">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-white font-semibold">{expectation.name || '(unnamed)'}</h3>
                    <span className={`inline-flex px-2 py-1 rounded text-xs border ${expectation.enabled ? 'bg-green-500/15 text-green-300 border-green-500/30' : 'bg-gray-600/40 text-gray-300 border-gray-500/30'}`}>
                      {expectation.enabled ? 'Enabled' : 'Disabled'}
                    </span>
                    <span className="inline-flex px-2 py-1 rounded text-xs border bg-gray-700/60 border-gray-600 text-gray-300">
                      Env: {expectation.environment}
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-2 text-sm">
                    <span className={`inline-flex px-2 py-1 rounded border text-xs ${methodBadgeClass(expectation.requestMatcher?.method)}`}>
                      {expectation.requestMatcher?.method || 'ANY'}
                    </span>
                    <span className="font-mono text-gray-100">{expectation.requestMatcher?.path}</span>
                    <span className="text-xs text-gray-400">
                      {pathMatchTypeLabel(toIntEnum(expectation.requestMatcher?.pathMatchType as unknown, 0))}
                    </span>
                  </div>
                  <div className="flex flex-wrap gap-4 text-xs text-gray-400">
                    <span>Priority: <span className="text-gray-200">{expectation.priority}</span></span>
                    <span>Times: <span className="text-gray-200">{expectation.times?.unlimited ? 'Unlimited' : `${expectation.times?.remaining ?? 0} left`}</span></span>
                    <span>Response: <span className="text-gray-200">{expectation.responseTemplate?.status ?? 200}</span></span>
                    <span>Delay: <span className="text-gray-200">{expectation.responseTemplate?.delayMs ?? 0}ms</span></span>
                  </div>
                </div>
                <div className="flex flex-wrap items-center gap-2 lg:justify-end">
                  <button
                    onClick={() => toggleEnabled(expectation)}
                    disabled={!canWriteMocks}
                    className="inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded border border-gray-600 bg-gray-700 hover:bg-gray-600 text-xs text-gray-200 disabled:opacity-50"
                    title={expectation.enabled ? 'Disable' : 'Enable'}
                  >
                    {expectation.enabled ? <ToggleRight className="w-3.5 h-3.5" /> : <ToggleLeft className="w-3.5 h-3.5" />}
                    {expectation.enabled ? 'Disable' : 'Enable'}
                  </button>
                  <button
                    onClick={() => openEdit(expectation)}
                    disabled={!canWriteMocks}
                    className="inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded border border-gray-600 bg-gray-700 hover:bg-gray-600 text-xs text-gray-200 disabled:opacity-50"
                    title="Edit"
                  >
                    <Pencil className="w-3.5 h-3.5" />
                    Edit
                  </button>
                  <button
                    onClick={() => handleClone(expectation)}
                    disabled={!canWriteMocks}
                    className="inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded border border-gray-600 bg-gray-700 hover:bg-gray-600 text-xs text-gray-200 disabled:opacity-50"
                    title="Clone"
                  >
                    <Copy className="w-3.5 h-3.5" />
                    Clone
                  </button>
                  <button
                    onClick={() => handleDelete(expectation)}
                    disabled={!canWriteMocks}
                    className="inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded border border-red-500/40 bg-red-500/10 hover:bg-red-500/20 text-xs text-red-300 disabled:opacity-50"
                    title="Delete"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                    Delete
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {activeTab === 'logs' && (
        <div className="space-y-4">
          {!canReadLogs ? (
            <div className="p-6 rounded-lg border border-amber-500/40 bg-amber-500/10 text-amber-200">
              You do not have permission to read mock request logs.
            </div>
          ) : (
            <>
              <div className="bg-gray-800 border border-gray-700 rounded-lg p-4">
                <div className="grid grid-cols-1 md:grid-cols-12 gap-3">
                  <label className="text-sm text-gray-300 md:col-span-5">
                    Path contains
                    <input
                      type="text"
                      value={logsPathInput}
                      onChange={(e) => setLogsPathInput(e.target.value)}
                      placeholder="/orders"
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white font-mono"
                    />
                  </label>
                  <label className="text-sm text-gray-300 md:col-span-3">
                    Match status
                    <select
                      value={logsMatchedInput}
                      onChange={(e) => setLogsMatchedInput(e.target.value as 'all' | 'matched' | 'unmatched')}
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                    >
                      <option value="all">All</option>
                      <option value="matched">Matched only</option>
                      <option value="unmatched">Unmatched only</option>
                    </select>
                  </label>
                  <label className="text-sm text-gray-300 md:col-span-2">
                    Limit
                    <select
                      value={logsLimitInput}
                      onChange={(e) => setLogsLimitInput(Number(e.target.value))}
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                    >
                      <option value={25}>25</option>
                      <option value={50}>50</option>
                      <option value={100}>100</option>
                      <option value={200}>200</option>
                      <option value={500}>500</option>
                      <option value={1000}>1000</option>
                    </select>
                  </label>
                  <label className="text-sm text-gray-300 md:col-span-2">
                    Time range
                    <select
                      value={logsTimeRange}
                      onChange={(e) => setLogsTimeRange(e.target.value as LogTimeRange)}
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                    >
                      <option value="15m">Last 15m</option>
                      <option value="1h">Last 1h</option>
                      <option value="6h">Last 6h</option>
                      <option value="24h">Last 24h</option>
                      <option value="7d">Last 7d</option>
                    </select>
                  </label>
                  <div className="md:col-span-2 flex md:items-end gap-2">
                    <button
                      type="button"
                      onClick={applyLogsFilters}
                      className="w-full px-3 py-2 bg-gray-700 hover:bg-gray-600 rounded text-white text-sm"
                    >
                      Apply
                    </button>
                    <button
                      type="button"
                      onClick={resetLogsFilters}
                      className="w-full px-3 py-2 bg-gray-700 hover:bg-gray-600 rounded text-white text-sm"
                    >
                      Reset
                    </button>
                    <button
                      type="button"
                      onClick={handleClearLogs}
                      disabled={!canDeleteLogs || isClearingLogs}
                      className="w-full px-3 py-2 bg-red-600/80 hover:bg-red-600 rounded text-white text-sm disabled:opacity-50"
                    >
                      {isClearingLogs ? 'Clearing...' : 'Clear'}
                    </button>
                  </div>
                  <div className="md:col-span-12 text-xs text-gray-400">
                    Applied filters: path <span className="font-mono text-gray-300">{logsPathFilter || '(any)'}</span> · status <span className="text-gray-300">{logsMatchedFilter}</span> · limit <span className="text-gray-300">{logsLimit}</span>
                  </div>
                </div>
              </div>

              <div className="bg-gray-800 border border-gray-700 rounded-lg p-4 space-y-4">
                <div className="flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
                  <div>
                    <h3 className="text-white font-semibold">Request Usage</h3>
                    <p className="text-xs text-gray-400">
                      {logRangeMeta[logsTimeRange].label} · Bucketed over {logChart.range.buckets} intervals
                    </p>
                    <div className="mt-2 flex flex-wrap gap-2">
                      {(Object.keys(logRangeMeta) as LogTimeRange[]).map((rangeKey) => (
                        <button
                          key={rangeKey}
                          type="button"
                          onClick={() => setLogsTimeRange(rangeKey)}
                          className={`px-2 py-1 rounded text-xs border ${logsTimeRange === rangeKey ? 'border-cyan-500/40 bg-cyan-500/15 text-cyan-300' : 'border-gray-600 bg-gray-700 text-gray-300 hover:bg-gray-600'}`}
                        >
                          {logRangeMeta[rangeKey].label}
                        </button>
                      ))}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2 text-xs">
                    <span className="px-2 py-1 rounded border border-cyan-500/40 bg-cyan-500/10 text-cyan-300">Total: {logChart.totalInRange}</span>
                    <span className="px-2 py-1 rounded border border-green-500/40 bg-green-500/10 text-green-300">Matched: {logChart.matchedInRange}</span>
                    <span className="px-2 py-1 rounded border border-amber-500/40 bg-amber-500/10 text-amber-300">Unmatched: {logChart.unmatchedInRange}</span>
                  </div>
                </div>

                {logChart.totalInRange === 0 ? (
                  <div className="h-52 flex items-center justify-center text-sm text-gray-400 border border-dashed border-gray-600 rounded">
                    No log events in selected time range.
                  </div>
                ) : (
                  <div className="w-full overflow-x-auto">
                    <svg viewBox="0 0 1000 260" className="w-full min-w-[780px] h-[260px]">
                      {(() => {
                        const chartLeft = 56;
                        const chartRight = 20;
                        const chartTop = 20;
                        const chartBottom = 40;
                        const chartWidth = 1000 - chartLeft - chartRight;
                        const chartHeight = 260 - chartTop - chartBottom;
                        const safeDenominator = Math.max(1, logChart.buckets.length - 1);
                        const xFor = (index: number) => chartLeft + (index / safeDenominator) * chartWidth;
                        const yFor = (value: number) => chartTop + chartHeight - (value / logChart.maxValue) * chartHeight;

                        const totalLine = logChart.buckets.map((bucket, index) => `${xFor(index)},${yFor(bucket.total)}`).join(' ');
                        const matchedLine = logChart.buckets.map((bucket, index) => `${xFor(index)},${yFor(bucket.matched)}`).join(' ');
                        const unmatchedLine = logChart.buckets.map((bucket, index) => `${xFor(index)},${yFor(bucket.unmatched)}`).join(' ');
                        const totalArea = `${totalLine} ${xFor(logChart.buckets.length - 1)},${chartTop + chartHeight} ${xFor(0)},${chartTop + chartHeight}`;
                        const yTicks = [0, 0.25, 0.5, 0.75, 1];
                        const xTicks = [0, 0.2, 0.4, 0.6, 0.8, 1];

                        return (
                          <>
                            {yTicks.map((tick) => {
                              const y = chartTop + chartHeight - tick * chartHeight;
                              const value = Math.round(logChart.maxValue * tick);
                              return (
                                <g key={`y-${tick}`}>
                                  <line x1={chartLeft} y1={y} x2={chartLeft + chartWidth} y2={y} stroke="rgba(107,114,128,0.35)" strokeDasharray="4 6" />
                                  <text x={chartLeft - 8} y={y + 4} fill="#9ca3af" fontSize="11" textAnchor="end">{value}</text>
                                </g>
                              );
                            })}

                            {xTicks.map((tick) => {
                              const x = chartLeft + tick * chartWidth;
                              const at = logChart.start + tick * logChart.range.durationMs;
                              return (
                                <g key={`x-${tick}`}>
                                  <line x1={x} y1={chartTop} x2={x} y2={chartTop + chartHeight} stroke="rgba(75,85,99,0.2)" />
                                  <text x={x} y={chartTop + chartHeight + 18} fill="#9ca3af" fontSize="11" textAnchor="middle">
                                    {formatTimeAxisLabel(at, logsTimeRange)}
                                  </text>
                                </g>
                              );
                            })}

                            <polygon points={totalArea} fill="rgba(34,211,238,0.12)" />
                            {showTotalSeries && <polyline fill="none" stroke="#22d3ee" strokeWidth="2.5" points={totalLine} />}
                            {showMatchedSeries && <polyline fill="none" stroke="#4ade80" strokeWidth="2" points={matchedLine} />}
                            {showUnmatchedSeries && <polyline fill="none" stroke="#f59e0b" strokeWidth="2" points={unmatchedLine} />}

                            <text x={chartLeft} y={12} fill="#9ca3af" fontSize="11">Requests per bucket</text>
                          </>
                        );
                      })()}
                    </svg>
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2 text-xs text-gray-300">
                  <button
                    type="button"
                    onClick={() => setShowTotalSeries((prev) => !prev)}
                    className={`inline-flex items-center gap-2 px-2 py-1 rounded border ${showTotalSeries ? 'border-cyan-500/40 bg-cyan-500/10 text-cyan-300' : 'border-gray-600 bg-gray-700 text-gray-400'}`}
                  >
                    <span className="w-3 h-0.5 bg-cyan-400" />
                    Total
                  </button>
                  <button
                    type="button"
                    onClick={() => setShowMatchedSeries((prev) => !prev)}
                    className={`inline-flex items-center gap-2 px-2 py-1 rounded border ${showMatchedSeries ? 'border-green-500/40 bg-green-500/10 text-green-300' : 'border-gray-600 bg-gray-700 text-gray-400'}`}
                  >
                    <span className="w-3 h-0.5 bg-green-400" />
                    Matched
                  </button>
                  <button
                    type="button"
                    onClick={() => setShowUnmatchedSeries((prev) => !prev)}
                    className={`inline-flex items-center gap-2 px-2 py-1 rounded border ${showUnmatchedSeries ? 'border-amber-500/40 bg-amber-500/10 text-amber-300' : 'border-gray-600 bg-gray-700 text-gray-400'}`}
                  >
                    <span className="w-3 h-0.5 bg-amber-400" />
                    Unmatched
                  </button>
                </div>
              </div>

              <div className="bg-gray-800 border border-gray-700 rounded-lg overflow-x-auto">
                <table className="w-full min-w-[1100px]">
                  <thead className="bg-gray-700/50">
                    <tr>
                      <th className="text-left px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Timestamp</th>
                      <th className="text-center px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Method</th>
                      <th className="text-left px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Path</th>
                      <th className="text-center px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Matched</th>
                      <th className="text-left px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Expectation</th>
                      <th className="text-center px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Response</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-700">
                    {isLoadingLogs && (
                      <tr>
                        <td colSpan={6} className="px-4 py-8 text-center text-gray-400">Loading request logs...</td>
                      </tr>
                    )}
                    {!isLoadingLogs && logs.length === 0 && (
                      <tr>
                        <td colSpan={6} className="px-4 py-8 text-center text-gray-400">No request logs found for current filters.</td>
                      </tr>
                    )}
                    {logs.map((log) => (
                      <tr
                        key={log.id}
                        className="hover:bg-gray-700/30 cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400/70 focus-visible:ring-inset"
                        onClick={() => setSelectedLog(log)}
                        tabIndex={0}
                        role="button"
                        aria-label={`Open request log ${log.method || 'ANY'} ${log.path}`}
                        onKeyDown={(event) => {
                          if (event.key === 'Enter' || event.key === ' ') {
                            event.preventDefault();
                            setSelectedLog(log);
                          }
                        }}
                      >
                        <td className="px-4 py-3 text-sm text-gray-200 whitespace-nowrap">{formatTimestamp(log.timestamp)}</td>
                        <td className="px-4 py-3 text-sm text-center">
                          <span className={`inline-flex px-2 py-1 rounded border text-xs ${methodBadgeClass(log.method)}`}>
                            {log.method || 'ANY'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-200">
                          <span className="font-mono">{log.path}</span>
                          {log.queryString ? <span className="text-xs text-gray-400 ml-2">{log.queryString}</span> : null}
                        </td>
                        <td className="px-4 py-3 text-center">
                          <span className={`inline-flex px-2 py-1 rounded text-xs border ${log.matched ? 'bg-green-500/15 text-green-300 border-green-500/30' : 'bg-amber-500/15 text-amber-300 border-amber-500/30'}`}>
                            {log.matched ? 'Matched' : 'Unmatched'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-200">
                          {log.matchedExpectationName || '-'}
                        </td>
                        <td className="px-4 py-3 text-sm text-center text-gray-200">
                          {log.responseStatusCode}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      )}

      {showModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="mock-expectation-dialog-title"
            className="bg-gray-800 border border-gray-700 rounded-lg w-full max-w-5xl max-h-[92vh] overflow-hidden"
          >
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-700">
              <h2 id="mock-expectation-dialog-title" className="text-xl text-white font-semibold">{editingId ? 'Edit Expectation' : 'Create Expectation'}</h2>
              <button type="button" onClick={closeModal} aria-label="Close expectation dialog" className="p-2 rounded hover:bg-gray-700">
                <X className="w-5 h-5 text-gray-300" />
              </button>
            </div>
            <form onSubmit={handleSave} className="p-6 space-y-6 overflow-y-auto max-h-[calc(92vh-76px)]">
              <div className="flex flex-wrap gap-2 text-xs">
                <span className="px-2 py-1 rounded border border-cyan-500/30 bg-cyan-500/10 text-cyan-300">1. General</span>
                <span className="px-2 py-1 rounded border border-blue-500/30 bg-blue-500/10 text-blue-300">2. Request Matching</span>
                <span className="px-2 py-1 rounded border border-indigo-500/30 bg-indigo-500/10 text-indigo-300">3. Response</span>
                <span className="px-2 py-1 rounded border border-emerald-500/30 bg-emerald-500/10 text-emerald-300">4. Execution Limits</span>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-4">
                <h3 className="text-white font-medium">General</h3>
                <p className="text-xs text-gray-400">Identity and targeting configuration for this expectation.</p>
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <label className="text-sm text-gray-300">
                    Name
                    <input type="text" value={form.name} onChange={(e) => setField('name', e.target.value)} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" required />
                  </label>
                  <label className="text-sm text-gray-300">
                    Environment
                    <select
                      value={form.environment}
                      onChange={(e) => setField('environment', e.target.value)}
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                      required
                      disabled={environmentOptions.length === 0}
                    >
                      {environmentOptions.length === 0 ? (
                        <option value="">No environments</option>
                      ) : (
                        environmentOptions.map((envName) => (
                          <option key={envName} value={envName}>{envName}</option>
                        ))
                      )}
                    </select>
                  </label>
                  <label className="text-sm text-gray-300">
                    Priority
                    <input type="number" value={form.priority} onChange={(e) => setField('priority', Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                  </label>
                  <label className="text-sm text-gray-300 flex items-center gap-2 mt-7">
                    <input type="checkbox" checked={form.enabled} onChange={(e) => setField('enabled', e.target.checked)} />
                    Enabled
                  </label>
                </div>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-4">
                <h3 className="text-white font-medium">Request Matching</h3>
                <p className="text-xs text-gray-400">Define incoming request conditions that should trigger this response.</p>
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <label className="text-sm text-gray-300">
                    Method
                    <select value={form.method} onChange={(e) => setField('method', e.target.value)} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white">
                      {['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].map((method) => <option key={method} value={method}>{method}</option>)}
                    </select>
                  </label>
                  <label className="text-sm text-gray-300 md:col-span-2">
                    Path
                    <input type="text" value={form.path} onChange={(e) => setField('path', e.target.value)} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white font-mono" required />
                  </label>
                  <label className="text-sm text-gray-300">
                    Path Match
                    <select value={form.pathMatchType} onChange={(e) => setField('pathMatchType', Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white">
                      <option value={0}>Exact</option>
                      <option value={1}>Prefix</option>
                      <option value={2}>Regex</option>
                    </select>
                  </label>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <p className="text-xs uppercase tracking-wider text-gray-500">Query Params</p>
                      <button type="button" onClick={() => addRow('queryRows')} className="text-xs text-blue-300 hover:text-blue-200">+ Add</button>
                    </div>
                    <div className="space-y-2">
                      {form.queryRows.map((row, index) => (
                        <div key={`q-${index}`} className="grid grid-cols-[1fr_1fr_auto] gap-2">
                          <input value={row.key} onChange={(e) => setRow('queryRows', index, 'key', e.target.value)} placeholder="key" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                          <input value={row.value} onChange={(e) => setRow('queryRows', index, 'value', e.target.value)} placeholder="value" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                          <button type="button" onClick={() => removeRow('queryRows', index)} className="px-2 text-red-300 hover:text-red-200">x</button>
                        </div>
                      ))}
                    </div>
                  </div>
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <p className="text-xs uppercase tracking-wider text-gray-500">Headers</p>
                      <button type="button" onClick={() => addRow('headerRows')} className="text-xs text-blue-300 hover:text-blue-200">+ Add</button>
                    </div>
                    <div className="space-y-2">
                      {form.headerRows.map((row, index) => (
                        <div key={`h-${index}`} className="grid grid-cols-[1fr_1fr_auto] gap-2">
                          <input value={row.key} onChange={(e) => setRow('headerRows', index, 'key', e.target.value)} placeholder="key" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                          <input value={row.value} onChange={(e) => setRow('headerRows', index, 'value', e.target.value)} placeholder="value" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                          <button type="button" onClick={() => removeRow('headerRows', index)} className="px-2 text-red-300 hover:text-red-200">x</button>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <label className="text-sm text-gray-300">
                    Body Match
                    <select value={form.bodyMatchType} onChange={(e) => setField('bodyMatchType', Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white">
                      <option value={0}>Any</option>
                      <option value={1}>Exact</option>
                      <option value={2}>Contains</option>
                      <option value={3}>Regex</option>
                    </select>
                  </label>
                  <label className="text-sm text-gray-300 md:col-span-2">
                    Body Pattern
                    <textarea value={form.body} onChange={(e) => setField('body', e.target.value)} rows={3} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white font-mono" />
                  </label>
                </div>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-4">
                <h3 className="text-white font-medium">Response</h3>
                <p className="text-xs text-gray-400">Configure status, headers, and payload returned to matched requests.</p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <label className="text-sm text-gray-300">
                    Status
                    <input type="number" value={form.status} onChange={(e) => setField('status', Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                  </label>
                  <label className="text-sm text-gray-300">
                    Delay (ms)
                    <input type="number" min={0} value={form.delayMs} onChange={(e) => setField('delayMs', Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                  </label>
                </div>
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <p className="text-xs uppercase tracking-wider text-gray-500">Response Headers</p>
                    <button type="button" onClick={() => addRow('responseHeaderRows')} className="text-xs text-blue-300 hover:text-blue-200">+ Add</button>
                  </div>
                  <div className="space-y-2">
                    {form.responseHeaderRows.map((row, index) => (
                      <div key={`rh-${index}`} className="grid grid-cols-[1fr_1fr_auto] gap-2">
                        <input value={row.key} onChange={(e) => setRow('responseHeaderRows', index, 'key', e.target.value)} placeholder="key" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                        <input value={row.value} onChange={(e) => setRow('responseHeaderRows', index, 'value', e.target.value)} placeholder="value" className="px-2 py-1.5 bg-gray-700 border border-gray-600 rounded text-sm text-white" />
                        <button type="button" onClick={() => removeRow('responseHeaderRows', index)} className="px-2 text-red-300 hover:text-red-200">x</button>
                      </div>
                    ))}
                  </div>
                </div>
                <label className="text-sm text-gray-300 block">
                  Response Body
                  <textarea value={form.responseBody} onChange={(e) => setField('responseBody', e.target.value)} rows={5} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white font-mono" />
                </label>
              </div>

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4 space-y-4">
                <h3 className="text-white font-medium">Execution Limits</h3>
                <p className="text-xs text-gray-400">Control whether the expectation can be consumed infinitely or a fixed number of times.</p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <label className="text-sm text-gray-300 flex items-center gap-2 mt-7">
                    <input type="checkbox" checked={form.unlimited} onChange={(e) => setField('unlimited', e.target.checked)} />
                    Unlimited calls
                  </label>
                  {!form.unlimited && (
                    <label className="text-sm text-gray-300">
                      Remaining
                      <input type="number" min={1} value={form.remaining} onChange={(e) => setField('remaining', Number(e.target.value))} className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white" />
                    </label>
                  )}
                </div>
              </div>

              <div className="flex justify-end gap-3">
                <button type="button" onClick={closeModal} className="px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded text-white">
                  Cancel
                </button>
                <button type="submit" disabled={isSaving} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded text-white disabled:opacity-50">
                  {isSaving ? 'Saving...' : editingId ? 'Save Changes' : 'Create Expectation'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {activeTab === 'logs' && selectedLog && (
        <div className="fixed inset-0 z-50 bg-black/40 flex justify-end" onClick={() => setSelectedLog(null)}>
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="mock-log-detail-title"
            className="h-full w-full max-w-2xl bg-gray-900 border-l border-gray-700 p-6 overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between mb-4">
              <h3 id="mock-log-detail-title" className="text-lg font-semibold text-white">Request Log Details</h3>
              <button type="button" onClick={() => setSelectedLog(null)} aria-label="Close request log details" className="p-2 rounded hover:bg-gray-800 text-gray-300">
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm mb-4">
              <div className="rounded border border-gray-700 bg-gray-800 p-3">
                <p className="text-gray-400 text-xs mb-1">Timestamp</p>
                <p className="text-gray-100">{formatTimestamp(selectedLog.timestamp)}</p>
              </div>
              <div className="rounded border border-gray-700 bg-gray-800 p-3">
                <p className="text-gray-400 text-xs mb-1">Environment</p>
                <p className="text-gray-100">{selectedLog.environment}</p>
              </div>
              <div className="rounded border border-gray-700 bg-gray-800 p-3">
                <p className="text-gray-400 text-xs mb-1">Method</p>
                <span className={`inline-flex px-2 py-1 rounded border text-xs ${methodBadgeClass(selectedLog.method)}`}>
                  {selectedLog.method || 'ANY'}
                </span>
              </div>
              <div className="rounded border border-gray-700 bg-gray-800 p-3">
                <p className="text-gray-400 text-xs mb-1">Matched</p>
                <span className={`inline-flex px-2 py-1 rounded text-xs border ${selectedLog.matched ? 'bg-green-500/15 text-green-300 border-green-500/30' : 'bg-amber-500/15 text-amber-300 border-amber-500/30'}`}>
                  {selectedLog.matched ? 'Matched' : 'Unmatched'}
                </span>
              </div>
            </div>
            <div className="rounded border border-gray-700 bg-gray-800 p-3 mb-3">
              <p className="text-gray-400 text-xs mb-1">Path</p>
              <p className="font-mono text-gray-100 break-all">{selectedLog.path}</p>
            </div>
            <div className="rounded border border-gray-700 bg-gray-800 p-3 mb-3">
              <p className="text-gray-400 text-xs mb-1">Query String</p>
              <p className="font-mono text-gray-100 break-all">{selectedLog.queryString || '-'}</p>
            </div>
            <div className="rounded border border-gray-700 bg-gray-800 p-3 mb-3">
              <p className="text-gray-400 text-xs mb-1">Matched Expectation</p>
              <p className="text-gray-100">{selectedLog.matchedExpectationName || '-'}</p>
            </div>
            <div className="rounded border border-gray-700 bg-gray-800 p-3 mb-3">
              <p className="text-gray-400 text-xs mb-1">Response Status</p>
              <p className="text-gray-100">{selectedLog.responseStatusCode}</p>
            </div>
            <div className="rounded border border-gray-700 bg-gray-800 p-3 mb-3">
              <p className="text-gray-400 text-xs mb-1">Headers</p>
              <pre className="text-xs text-gray-200 overflow-x-auto">{JSON.stringify(selectedLog.headers || {}, null, 2)}</pre>
            </div>
            <div className="rounded border border-gray-700 bg-gray-800 p-3">
              <p className="text-gray-400 text-xs mb-1">Body</p>
              <pre className="text-xs text-gray-200 whitespace-pre-wrap break-words">{selectedLog.body || '-'}</pre>
            </div>
          </div>
        </div>
      )}
      <p className="text-xs text-gray-500">
        Tip: runtime mock URLs use <span className="font-mono">/mock/{environment}/path</span>. Example: <span className="font-mono">/mock/{environment}/orders/123</span>.
      </p>
      <p className="text-xs text-gray-500">
        Matchers: Path={pathMatchTypeLabel(form.pathMatchType)}, Body={bodyMatchTypeLabel(form.bodyMatchType)}.
      </p>
    </div>
  );
};

export default Mocks;
