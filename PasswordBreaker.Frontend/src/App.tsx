import { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import axios from 'axios';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { Activity, Play, ShieldAlert, Cpu, CheckCircle } from 'lucide-react';

interface AttackStatus {
  isActive: boolean;
  foundPassword: string | null;
  targetHash: string;
  totalHashesComputed: number;
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
  
  // Form State
  const [targetHash, setTargetHash] = useState('a9c449d4fa44e9e5a41c574ae55ce4d9');
  const [hashType, setHashType] = useState('MD5');
  const [alphabet, setAlphabet] = useState('abcdefghijklmnopqrstuvwxyz');
  const [maxLength, setMaxLength] = useState(7);

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
    }).catch(err => console.error("Could not fetch initial status", err));

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
    });

    connection.on('StatsUpdated', (totalHashes: number) => {
      latestTotalRef.current = totalHashes;
      setStatus(s => s ? { ...s, totalHashesComputed: totalHashes } : null);
    });

    connection.on('AttackFinished', (finalStatus: AttackStatus) => {
      setStatus(finalStatus);
      isActiveRef.current = false;
      latestTotalRef.current = finalStatus.totalHashesComputed;
    });

    connection.start()
      .then(() => console.log('Connected to DashboardHub'))
      .catch(err => console.error('SignalR Connection Error: ', err));

    return () => {
      connection.stop();
    };
  }, []);

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

  const isSuccess = !status?.isActive && status?.foundPassword != null && status?.foundPassword !== "[ATTACK STOPPED BY USER]";
  const isStopped = !status?.isActive && status?.foundPassword === "[ATTACK STOPPED BY USER]";

  return (
    <div className="min-h-screen p-8">
      <div className="max-w-6xl mx-auto space-y-8">
        
        <header className="flex items-center justify-between border-b border-slate-800 pb-6">
          <div className="flex items-center gap-3">
            <ShieldAlert className="w-8 h-8 text-red-500" />
            <h1 className="text-3xl font-bold tracking-tight">Password Breaker</h1>
          </div>
          <div className="flex items-center gap-2">
            <div className={`w-3 h-3 rounded-full ${status?.isActive ? 'bg-green-500 animate-pulse' : 'bg-slate-600'}`} />
            <span className="text-sm font-medium text-slate-400">
              {status?.isActive ? 'ATTACK IN PROGRESS' : 'IDLE'}
            </span>
          </div>
        </header>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          
          <div className="space-y-6">
            <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700">
              <h2 className="text-xl font-semibold mb-4 flex items-center gap-2">
                <Play className="w-5 h-5 text-blue-400" /> New Attack
              </h2>
              
              <form onSubmit={handleStartAttack} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-slate-400 mb-1">Target Hash</label>
                  <input 
                    type="text" 
                    required
                    disabled={status?.isActive}
                    className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500"
                    value={targetHash}
                    onChange={e => setTargetHash(e.target.value)}
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-400 mb-1">Algorithm</label>
                    <select 
                      disabled={status?.isActive}
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500"
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
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500"
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
                    className="w-full bg-slate-900 border border-slate-700 rounded-lg px-4 py-2 text-sm focus:outline-none focus:border-blue-500"
                    value={alphabet}
                    onChange={e => setAlphabet(e.target.value)}
                  />
                </div>

                <button 
                  type="submit" 
                  disabled={status?.isActive}
                  className="w-full bg-blue-600 hover:bg-blue-500 disabled:bg-slate-700 disabled:text-slate-500 text-white font-semibold py-3 rounded-lg transition-colors flex items-center justify-center gap-2"
                >
                  <Activity className="w-5 h-5" /> Launch Attack
                </button>

                {status?.isActive && (
                  <button 
                    type="button"
                    onClick={handleStopAttack}
                    className="w-full bg-red-600 hover:bg-red-500 text-white font-semibold py-3 rounded-lg transition-colors flex items-center justify-center gap-2 mt-2"
                  >
                    <ShieldAlert className="w-5 h-5" /> Stop Attack
                  </button>
                )}
              </form>
            </div>
          </div>

          <div className="lg:col-span-2 space-y-6">
            
            <div className="grid grid-cols-2 gap-4">
              <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700 flex flex-col justify-center">
                <span className="text-sm font-medium text-slate-400 mb-1">Current Speed</span>
                <div className="text-3xl font-bold flex items-end gap-2">
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

            {isSuccess && (
              <div className="bg-green-500/10 border border-green-500/50 rounded-xl p-6 flex items-start gap-4">
                <CheckCircle className="w-8 h-8 text-green-500 shrink-0" />
                <div>
                  <h3 className="text-lg font-semibold text-green-400">Password Cracked Successfully!</h3>
                  <div className="mt-2 font-mono text-2xl bg-black/30 px-4 py-2 rounded border border-green-500/20 inline-block text-green-300">
                    {status?.foundPassword}
                  </div>
                </div>
              </div>
            )}

            {isStopped && (
              <div className="bg-yellow-500/10 border border-yellow-500/50 rounded-xl p-6 flex items-start gap-4">
                <ShieldAlert className="w-8 h-8 text-yellow-500 shrink-0" />
                <div>
                  <h3 className="text-lg font-semibold text-yellow-400">Attack Cancelled</h3>
                  <p className="text-yellow-200/70 text-sm mt-1">The process was manually terminated by the operator.</p>
                </div>
              </div>
            )}

            <div className="bg-slate-800/50 p-6 rounded-xl border border-slate-700 overflow-hidden">
              <h2 className="text-xl font-semibold mb-6 flex items-center gap-2">
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
        </div>
      </div>
    </div>
  );
}

export default App;
