-- Initialize TimescaleDB extension
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- trades table
CREATE TABLE IF NOT EXISTS trades (
  trade_id BIGINT PRIMARY KEY,
  contract_id INT NOT NULL,
  customer_id INT NOT NULL,
  book_id INT NOT NULL,
  trader_id NUMERIC NOT NULL,
  department_id INT,
  trade_date TIMESTAMP WITH TIME ZONE NOT NULL,
  time_updated TIMESTAMP WITH TIME ZONE NOT NULL,
  volume NUMERIC NOT NULL,
  price NUMERIC NOT NULL,
  currency TEXT,
  side TEXT,
  counterparty_id INT,
  delivery_start TIMESTAMP WITH TIME ZONE,
  delivery_end TIMESTAMP WITH TIME ZONE,
  product_type TEXT,
  source TEXT
);

-- Convert trades to hypertable
SELECT create_hypertable('trades', 'trade_date', if_not_exists => TRUE);

-- eod_prices table
CREATE TABLE IF NOT EXISTS eod_prices (
  id BIGSERIAL PRIMARY KEY,
  contract_id INT NOT NULL,
  customer_id INT,
  trading_period TIMESTAMP WITH TIME ZONE NOT NULL,
  publication_time TIMESTAMP WITH TIME ZONE,
  price NUMERIC NOT NULL,
  currency TEXT,
  price_source TEXT,
  market_zone TEXT
);

-- Convert eod_prices to hypertable
SELECT create_hypertable('eod_prices', 'trading_period', if_not_exists => TRUE);

-- positions table
CREATE TABLE IF NOT EXISTS positions (
  position_id BIGSERIAL PRIMARY KEY,
  contract_id INT NOT NULL,
  customer_id INT NOT NULL,
  book_id INT NOT NULL,
  trader_id NUMERIC NOT NULL,
  department_id INT,
  time_updated TIMESTAMP WITH TIME ZONE NOT NULL,
  volume NUMERIC NOT NULL,
  product_type TEXT,
  currency TEXT,
  side TEXT,
  source TEXT,
  UNIQUE(contract_id, customer_id, book_id, trader_id, department_id, product_type, currency, side)
);

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS idx_trades_contract ON trades(contract_id);
CREATE INDEX IF NOT EXISTS idx_trades_customer ON trades(customer_id);
CREATE INDEX IF NOT EXISTS idx_eod_contract ON eod_prices(contract_id);
CREATE INDEX IF NOT EXISTS idx_positions_contract ON positions(contract_id);
