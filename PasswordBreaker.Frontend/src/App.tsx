import { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import axios from 'axios';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { Activity, Play, ShieldAlert, Cpu, CheckCircle, Database, LayoutDashboard } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import HistoryView from './components/HistoryView';

interface AttackStatus {
  isActive: boolean;
  foundPassword: string | null;
  targetHash: string;
  totalHashesComputed: number;
  connectedWorkers: number;
  startTime: string;
  endTime: string | null;
}

interface ChartData {
  time: string;
  hps: number;
}

const API_URL = 'http://localhost:15000';

function App() {
  const [status, setStatus] = useState<AttackStatus | null>(null);
  const [chartData, setChartData] = useState<ChartData[]>([]);
  const [currentHps, setCurrentHps] = useState(0);
  const [elapsedMs, setElapsedMs] = useState(0);
  const [activeTab, setActiveTab] = useState<'dashboard' | 'history'>('dashboard');
  
  // Form State
  const [targetHash, setTargetHash] = useState('a9c449d4fa44e9e5a41c574ae55ce4d9');
  const [hashType, setHashType] = useState('MD5');
  const [alphabet, setAlphabet] = useState('abcdefghijklmnopqrstuvwxyz');
  const [maxLength, setMaxLength] = useState(7);
  const [desiredWorkers, setDesiredWorkers] = useState(0);

  // Refs for interval decoupling
  const latestTotalRef = useRef(0);
  const isActiveRef = useRef(false);
  const prevHashesRef = useRef(0);

  useEffect(() => {
    // Initial fetch
    axios.get(`${API_URL}/api/attack/status`).then(res => {
      setStatus(res.data);
      latestTotalRef.current = res.data.totalHashesComputed;
      isActiveRef.current = res.data.isActive;
      prevHashesRef.current = res.data.totalHashesComputed;
      
      if (res.data.isActive && res.data.startTime) {
        const start = new Date(res.data.startTime).getTime();
        setElapsedMs(Date.now() - start);
      } else if (res.data.startTime && res.data.endTime) {
        const start = new Date(res.data.startTime).getTime();
        const end = new Date(res.data.endTime).getTime();
        setElapsedMs(end - start);
      }
    }).catch(err => console.error("Could not fetch initial status", err));

    // Fetch initial worker count
    axios.get(`${API_URL}/api/workers/count`).then(res => {
      setDesiredWorkers(res.data.currentCount);
    }).catch(err => console.error("Could not fetch worker count", err));

    // SignalR Connection
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/dashboardHub`)
      .withAutomaticReconnect()
      .build();

    connection.on('AttackStarted', (newStatus: AttackStatus) => {
      setStatus(newStatus);
      isActiveRef.current = true;
      latestTotalRef.current = 0;
      prevHashesRef.current = 0;
      setChartData([]);
      setCurrentHps(0);
      setElapsedMs(0);
    });

    connection.on('StatsUpdated', (totalHashes: number) => {
      latestTotalRef.current = totalHashes;
      setStatus(s => s ? { ...s, totalHashesComputed: totalHashes } : null);
    });

    connection.on('AttackFinished', (finalStatus: AttackStatus) => {
      setStatus(finalStatus);
      isActiveRef.current = false;
      latestTotalRef.current = finalStatus.totalHashesComputed;
      
      if (finalStatus.startTime && finalStatus.endTime) {
        const start = new Date(finalStatus.startTime).getTime();
        const end = new Date(finalStatus.endTime).getTime();
        setElapsedMs(end - start);
      }
    });

    connection.on('WorkerCountUpdated', (count: number) => {
      setStatus(s => s ? { ...s, connectedWorkers: count } : null);
    });

    connection.start()
      .then(() => console.log('Connected to DashboardHub'))
      .catch(err => console.error('SignalR Connection Error: ', err));

    return () => {
      connection.stop();
    };
  }, []);

  // Timer Effect
  useEffect(() => {
    let interval: number;
    if (status?.isActive) {
      interval = setInterval(() => {
        if (status.startTime) {
          const start = new Date(status.startTime).getTime();
          setElapsedMs(Date.now() - start);
        }
      }, 50); // High frequency for precise hundredths
    }
    return () => clearInterval(interval);
  }, [status?.isActive, status?.startTime]);

  // PŁYNNY WYKRES - aktualizacja co 200ms
  useEffect(() => {
    const UPDATE_INTERVAL_MS = 200;
    const MULTIPLIER_TO_SEC = 1000 / UPDATE_INTERVAL_MS;

    const interval = setInterval(() => {
      if (isActiveRef.current) {
        const currentTotal = latestTotalRef.current;
        const diff = currentTotal - prevHashesRef.current;
        prevHashesRef.current = currentTotal;

        const instantaneousHps = diff * MULTIPLIER_TO_SEC;

        setCurrentHps(prevHps => {
          const smoothedHps = prevHps === 0 ? instantaneousHps : Math.round((prevHps * 0.3) + (instantaneousHps * 0.7));
          
          setChartData(prev => {
            const now = new Date();
            const timeLabel = `${now.getSeconds()}.${Math.floor(now.getMilliseconds() / 100)}`; 
            const newData = [...prev, { time: timeLabel, hps: smoothedHps }];
            return newData.length > 60 ? newData.slice(newData.length - 60) : newData;
          });

          return smoothedHps;
        });
      } else {
        setCurrentHps(0);
      }
    }, UPDATE_INTERVAL_MS);

    return () => clearInterval(interval);
  }, []);

  const handleStartAttack = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await axios.post(`${API_URL}/api/attack/start`, {
        targetHash,
        hashType,
        alphabet,
        maxLength
      });
    } catch (error) {
      console.error("Failed to start attack", error);
      alert("Failed to start attack. Ensure the server is running.");
    }
  };

  const handleStopAttack = async () => {
    try {
      await axios.post(`${API_URL}/api/attack/stop`);
    } catch (error) {
      console.error("Failed to stop attack", error);
    }
  };

  const handleUpdateWorkers = async (newCount: number) => {
    if (newCount < 0) return;
    setDesiredWorkers(newCount);
    try {
      await axios.post(`${API_URL}/api/workers/count`, { count: newCount });
    } catch (error) {
      console.error("Failed to update worker count", error);
    }
  };

  const isSuccess = !status?.isActive && status?.foundPassword != null && status?.foundPassword !== "[ATTACK STOPPED BY USER]";
  const isStopped = !status?.isActive && status?.foundPassword === "[ATTACK STOPPED BY USER]";

  const formatDuration = (ms: number) => {
    const totalSeconds = Math.floor(ms / 1000);
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = totalSeconds % 60;
    const hundredths = Math.floor((ms % 1000) / 10);
    
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}.${hundredths.toString().padStart(2, '0')}`;
  };

  return (
    <div className="min-h-screen p-8 bg-slate-950 text-slate-200">
      <div className="max-w-6xl mx-auto space-y-8">
        
        <header className="flex items-center justify-between border-b border-slate-800 pb-6">
          <div className="flex items-center gap-6">
            <div className="flex items-center gap-3">
              <ShieldAlert className="w-8 h-8 text-red-500" />
              <h1 className="text-3xl font-bold tracking-tight text-white">Breaker</h1>
            </div>
            
            <nav className="flex items-center bg-slate-900/50 p-1 rounded-xl border border-slate-800">
              <button 
                onClick={() => setActiveTab('dashboard')}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${activeTab === 'dashboard' ? 'bg-blue-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'}`}
              >
                <LayoutDashboard className="w-4 h-4" /> Dashboard
              </button>
              <button 
                onClick={() => setActiveTab('history')}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${activeTab === 'history' ? 'bg-blue-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'}`}
              >
                <Database className="w-4 h-4" /> History
              </button>
            </nav>
          </div>

          <div className="flex items-center gap-6">
             <div className="flex flex-col items-end">
                <span className="text-[10px] font-bold text-slate-500 uppercase tracking-widest">Active Workers</span>
                <span className="text-xl font-mono font-bold text-blue-400">{status?.connectedWorkers || 0}</span>
             </div>
             <div className="h-8 w-[1px] bg-slate-800" />
             <div className="flex items-center gap-2">
                <div className={`w-3 h-3 rounded-full ${status?.isActive ? 'bg-green-500 animate-pulse' : 'bg-slate-600'}`} />
                <span className="text-sm font-medium text-slate-400">
                  {status?.isActive ? 'ATTACK IN PROGRESS' : 'IDLE'}
                </span>
             </div>
          </div>
        </header>

        <main className="relative">
          <AnimatePresence mode="wait">
            {activeTab === 'dashboard' ? (
              <motion.div 
                key="dashboard"
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 20 }}
                transition={{ duration: 0.2 }}
                className="grid grid-cols-1 lg:grid-cols-3 gap-8"
              >
                {/* Sidebar Controls */}
                <div className="space-y-6">
                  <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700">
                    <h2 className="text-xl font-semibold mb-4 flex items-center gap-2 text-white">
                      <Play className="w-5 h-5 text-blue-400" /> New Attack
                    </h2>
                    
                    <form onSubmit={handleStartAttack} className="space-y-4">
                      <div>
                        <label className="block text-sm font-medium text-slate-400 mb-1">Target Hash</label>
                        <input 
                          type="text" 
                          required
                          disabled={status?.isActive}
                          className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500 transition-colors"
                          value={targetHash}
                          onChange={e => setTargetHash(e.target.value)}
                        />
                      </div>

                      <div className="grid grid-cols-2 gap-4">
                        <div>
                          <label className="block text-sm font-medium text-slate-400 mb-1">Algorithm</label>
                          <select 
                            disabled={status?.isActive}
                            className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500 transition-colors"
                            value={hashType}
                            onChange={e => setHashType(e.target.value)}
                          >
                            <option value="MD5">MD5</option>
                            <option value="SHA256">SHA-256</option>
                            <option value="ARGON2">Argon2</option>
                          </select>
                        </div>
                        <div>
                          <label className="block text-sm font-medium text-slate-400 mb-1">Max Length</label>
                          <input 
                            type="number" 
                            min="1" max="12" required
                            disabled={status?.isActive}
                            className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500 transition-colors"
                            value={maxLength}
                            onChange={e => setMaxLength(parseInt(e.target.value))}
                          />
                        </div>
                      </div>

                      <div>
                        <label className="block text-sm font-medium text-slate-400 mb-1">Alphabet</label>
                        <input 
                          type="text" 
                          required
                          disabled={status?.isActive}
                          className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500 transition-colors"
                          value={alphabet}
                          onChange={e => setAlphabet(e.target.value)}
                        />
                      </div>

                      <button 
                        type="submit" 
                        disabled={status?.isActive}
                        className="w-full bg-blue-600 hover:bg-blue-500 disabled:bg-slate-700 disabled:text-slate-500 text-white font-semibold py-3 rounded-lg transition-all flex items-center justify-center gap-2 shadow-lg shadow-blue-900/20 active:scale-[0.98]"
                      >
                        <Activity className="w-5 h-5" /> Launch Attack
                      </button>

                      {status?.isActive && (
                        <button 
                          type="button"
                          onClick={handleStopAttack}
                          className="w-full bg-red-600/20 hover:bg-red-600/40 text-red-400 border border-red-500/20 font-semibold py-3 rounded-lg transition-all flex items-center justify-center gap-2 mt-2 active:scale-[0.98]"
                        >
                          <ShieldAlert className="w-5 h-5" /> Stop Attack
                        </button>
                      )}
                    </form>
                  </div>

                  <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700">
                    <h2 className="text-xl font-semibold mb-4 flex items-center gap-2 text-white">
                      <Cpu className="w-5 h-5 text-purple-400" /> Worker Management
                    </h2>
                    <div className="flex items-center justify-between gap-4">
                      <div className="flex flex-col">
                        <span className="text-sm font-medium text-slate-400">Target Worker Count</span>
                        <span className="text-xs text-slate-500 italic">Each worker uses 1 core</span>
                      </div>
                      <div className="flex items-center gap-3">
                        <button 
                          onClick={() => handleUpdateWorkers(desiredWorkers - 1)}
                          className="w-10 h-10 rounded-lg bg-slate-700 hover:bg-slate-600 flex items-center justify-center font-bold text-xl transition-colors active:scale-90"
                        >
                          -
                        </button>
                        <span className="w-8 text-center font-mono font-bold text-xl text-blue-400">
                          {desiredWorkers}
                        </span>
                        <button 
                          onClick={() => handleUpdateWorkers(desiredWorkers + 1)}
                          className="w-10 h-10 rounded-lg bg-slate-700 hover:bg-slate-600 flex items-center justify-center font-bold text-xl transition-colors active:scale-90"
                        >
                          +
                        </button>
                      </div>
                    </div>
                  </div>

                  <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700">
                    <span className="text-sm font-medium text-slate-400 mb-1 block">Attack Duration</span>
                    <div className="text-4xl font-mono font-bold text-white tracking-tighter">
                        {formatDuration(elapsedMs)}
                    </div>
                  </div>
                </div>

                {/* Main Content Area */}
                <div className="lg:col-span-2 space-y-6">
                  
                  <div className="grid grid-cols-2 gap-4">
                    <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700 flex flex-col justify-center">
                      <span className="text-sm font-medium text-slate-400 mb-1">Current Speed</span>
                      <div className="text-3xl font-bold flex items-end gap-2 text-white">
                        {currentHps.toLocaleString()} <span className="text-lg font-normal text-slate-500 mb-1">H/s</span>
                      </div>
                    </div>
                    
                    <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700 flex flex-col justify-center">
                      <span className="text-sm font-medium text-slate-400 mb-1">Total Checked</span>
                      <div className="text-3xl font-bold text-blue-400">
                        {status?.totalHashesComputed?.toLocaleString() || 0}
                      </div>
                    </div>
                  </div>

                  <AnimatePresence>
                    {isSuccess && (
                      <motion.div 
                        initial={{ opacity: 0, scale: 0.9 }}
                        animate={{ opacity: 1, scale: 1 }}
                        className="bg-green-500/10 border border-green-500/50 rounded-xl p-6 flex items-start gap-4 shadow-xl shadow-green-900/10"
                      >
                        <CheckCircle className="w-8 h-8 text-green-500 shrink-0" />
                        <div>
                          <h3 className="text-lg font-semibold text-green-400">Password Cracked Successfully!</h3>
                          <div className="mt-2 font-mono text-2xl bg-black/30 px-4 py-2 rounded border border-green-500/20 inline-block text-green-300">
                            {status?.foundPassword}
                          </div>
                        </div>
                      </motion.div>
                    )}

                    {isStopped && (
                      <motion.div 
                        initial={{ opacity: 0, scale: 0.9 }}
                        animate={{ opacity: 1, scale: 1 }}
                        className="bg-yellow-500/10 border border-yellow-500/50 rounded-xl p-6 flex items-start gap-4"
                      >
                        <ShieldAlert className="w-8 h-8 text-yellow-500 shrink-0" />
                        <div>
                          <h3 className="text-lg font-semibold text-yellow-400">Attack Cancelled</h3>
                          <p className="text-yellow-200/70 text-sm mt-1">The process was manually terminated by the operator.</p>
                        </div>
                      </motion.div>
                    )}
                  </AnimatePresence>

                  <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700 overflow-hidden">
                    <h2 className="text-xl font-semibold mb-6 flex items-center gap-2 text-white">
                      <Cpu className="w-5 h-5 text-purple-400" /> Performance History
                    </h2>
                    <div className="h-[300px] w-full" style={{ marginLeft: '-15px' }}>
                      <ResponsiveContainer width="100%" height="100%">
                        <LineChart data={chartData}>
                          <CartesianGrid strokeDasharray="3 3" stroke="#334155" vertical={false} />
                          <XAxis dataKey="time" hide={true} />
                          <YAxis 
                            stroke="#94a3b8" 
                            fontSize={12} 
                            width={100} 
                            tickFormatter={(v) => v.toLocaleString()}
                            axisLine={false}
                            tickLine={false}
                          />
                          <Tooltip 
                            contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', borderRadius: '0.5rem' }}
                            itemStyle={{ color: '#818cf8' }}
                            labelStyle={{ color: '#94a3b8' }}
                          />
                          <Line 
                            type="basis" 
                            dataKey="hps" 
                            stroke="#818cf8" 
                            strokeWidth={3}
                            dot={false}
                            isAnimationActive={false}
                          />
                        </LineChart>
                      </ResponsiveContainer>
                    </div>
                  </div>
                </div>
              </motion.div>
            ) : (
              <HistoryView key="history" />
            )}
          </AnimatePresence>
        </main>
      </div>
    </div>
  );
}

export default App;
