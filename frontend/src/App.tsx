import React, { useState } from 'react';
import { PosTerminalPage } from './presentation/pages/PosTerminalPage';
import { SalesHistoryPage } from './presentation/pages/SalesHistoryPage';

export const App: React.FC = () => {
  const [currentPage, setCurrentPage] = useState<'terminal' | 'history'>('terminal');

  return (
    <>
      {currentPage === 'terminal' ? (
        <PosTerminalPage onNavigateHistory={() => setCurrentPage('history')} />
      ) : (
        <SalesHistoryPage onBackToTerminal={() => setCurrentPage('terminal')} />
      )}
    </>
  );
};

export default App;
