import { useState, useEffect } from 'react';
import axios from 'axios';
import { motion, AnimatePresence } from 'framer-motion';
import { Clock, Key, Hash, Database, Cpu, FileText } from 'lucide-react';

interface CrackedPassword {
  id: number;
  hash: string;
  password: string;
  algorithm: string;
  workerCount: number;
  duration: string;
  crackedAt: string;
}

const API_URL = 'http://localhost:15000';

const HistoryView = () => {
  const [history, setHistory] = useState<CrackedPassword[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const fetchHistory = async () => {
      try {
        const response = await axios.get(`${API_URL}/api/history`);
        setHistory(response.data);
      } catch (error) {
        console.error("Failed to fetch history", error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchHistory();
    const interval = setInterval(fetchHistory, 10000); // Refresh every 10s
    return () => clearInterval(interval);
  }, []);

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  const formatDuration = (duration: string) => {
    if (!duration) return "N/A";
    // duration is in format "HH:mm:ss.SSSSSSS" or "d.HH:mm:ss.SSSSSSS"
    const parts = duration.split(':');
    if (parts.length < 3) return duration;
    
    const seconds = parseFloat(parts[2]);
    const minutes = parseInt(parts[1]);
    const hours = parseInt(parts[0]);
    
    if (hours > 0) return `${hours}h ${minutes}m ${seconds.toFixed(2)}s`;
    if (minutes > 0) return `${minutes}m ${seconds.toFixed(2)}s`;
    return `${seconds.toFixed(2)}s`;
  };

  const handleDownloadPdf = async (id: number, hash: string) => {
    try {
      const response = await axios.get(`${API_URL}/api/history/${id}/report`, {
        responseType: 'blob',
      });
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', `Report_${hash.substring(0, 8)}.pdf`);
      document.body.appendChild(link);
      link.click();
      link.remove();
    } catch (error) {
      console.error("Failed to download PDF", error);
    }
  };

  return (
    <motion.div 
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      className="space-y-6"
    >
      <div className="flex items-center justify-between mb-8">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white flex items-center gap-3">
            <Database className="text-blue-400" /> Crack History
          </h2>
          <p className="text-slate-400 mt-1">Archived results of successful decryption tasks.</p>
        </div>
        <div className="bg-blue-500/10 border border-blue-500/20 px-4 py-2 rounded-lg text-blue-400 text-sm font-medium">
          {history.length} records found
        </div>
      </div>

      <div className="bg-slate-800/50 rounded-2xl border border-slate-700 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-900/50 text-slate-400 text-xs uppercase tracking-wider">
                <th className="px-6 py-4 font-semibold">Algorithm</th>
                <th className="px-6 py-4 font-semibold">Hash</th>
                <th className="px-6 py-4 font-semibold">Result</th>
                <th className="px-6 py-4 font-semibold">Workers</th>
                <th className="px-6 py-4 font-semibold">Duration</th>
                <th className="px-6 py-4 font-semibold">Date</th>
                <th className="px-6 py-4 font-semibold text-center">Report</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-700/50">
              <AnimatePresence>
                {history.length === 0 && !isLoading ? (
                  <tr>
                    <td colSpan={7} className="px-6 py-12 text-center text-slate-500 italic">
                      No records in history yet. Launch an attack to start archiving.
                    </td>
                  </tr>
                ) : (
                  history.map((item, index) => (
                    <motion.tr 
                      key={item.id}
                      initial={{ opacity: 0, x: -10 }}
                      animate={{ opacity: 1, x: 0 }}
                      transition={{ delay: index * 0.05 }}
                      className="hover:bg-slate-700/30 transition-colors group"
                    >
                      <td className="px-6 py-4 text-sm">
                        <span className="bg-slate-900 px-2 py-1 rounded text-purple-400 font-mono text-xs border border-purple-500/20">
                          {item.algorithm}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2 text-slate-300">
                          <Hash className="w-4 h-4 text-slate-500 shrink-0" />
                          <span className="font-mono text-[10px] break-all">
                            {item.hash}
                          </span>
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2 text-green-400 font-bold">
                          <Key className="w-4 h-4" />
                          <span className="text-lg tracking-wide">{item.password}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2 text-blue-400 font-mono">
                          <Cpu className="w-4 h-4" />
                          <span>{item.workerCount}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2 text-amber-400 font-mono">
                          <Clock className="w-4 h-4" />
                          <span>{formatDuration(item.duration)}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-sm text-slate-400">
                        <div className="flex items-center gap-2">
                          <Clock className="w-4 h-4 opacity-50" />
                          {formatDate(item.crackedAt)}
                        </div>
                      </td>
                      <td className="px-6 py-4 text-center">
                        <button 
                          onClick={() => handleDownloadPdf(item.id, item.hash)}
                          className="p-2 rounded-lg bg-slate-900 hover:bg-blue-600 text-blue-400 hover:text-white border border-blue-500/20 transition-all group"
                          title="Download PDF Report"
                        >
                          <FileText className="w-5 h-5" />
                        </button>
                      </td>
                    </motion.tr>
                  ))
                )}
              </AnimatePresence>
            </tbody>
          </table>
        </div>
      </div>
    </motion.div>
  );
};

export default HistoryView;
