import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import Layout from './components/Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Users from './pages/Users';

function App() {
  return (
    <Router>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          
          <Route element={<Layout />}>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/users" element={<Users />} />
            
            <Route path="/roles" element={<div className="glass-panel" style={{padding: '2rem'}}>
                <h1>Roles & Permissions</h1><p>Module coming soon...</p>
            </div>} />
            <Route path="/sessions" element={<div className="glass-panel" style={{padding: '2rem'}}>
                <h1>Active Sessions</h1><p>Module coming soon...</p>
            </div>} />
          </Route>
        </Routes>
      </AuthProvider>
    </Router>
  );
}

export default App;
