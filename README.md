# 📈 ShareSync

### A Modern Share Market Portfolio Tracker

ShareSync is a full-stack web application designed to help investors track their share-market portfolios, transactions, watchlists, dividend income, and portfolio performance from a single dashboard.

The project combines a modern responsive web interface with a structured relational database and an ASP.NET Core backend.

## 🌐 Live Demo

👉 **[View ShareSync Live](https://mtanvira.github.io/ShareSync/)**

---

## 🚀 Project Overview

Managing investments across multiple companies can quickly become difficult when transactions, holdings, dividends, and portfolio performance are tracked manually.

**ShareSync** aims to solve this problem by providing a centralized platform where users can:

- 📊 Monitor portfolio performance
- 💰 Track investments and transactions
- 📈 Monitor individual companies
- ⭐ Maintain personalized watchlists
- 💵 Record and analyze dividend income
- 📉 Analyze portfolio performance through visual reports
- 🗃️ Store and manage investment data using a relational database

The project is being developed as a semester project with a strong focus on database design, full-stack development, and practical software engineering.

---

## ✨ Planned Features

### 📊 Dashboard
- Portfolio value overview
- Unrealized profit/loss
- Dividend income summary
- Recent transactions
- Portfolio performance overview

### 💼 Portfolio Management
- Create and manage portfolios
- Track holdings
- Calculate average purchase price
- Calculate current market value
- Calculate unrealized profit/loss

### 💸 Transaction Management
- Record BUY and SELL transactions
- Track quantity and price per share
- View transaction history
- Filter and organize transactions

### ⭐ Watchlist
- Add companies to a personal watchlist
- Set target prices
- Monitor current prices
- Track daily price changes

### 💰 Dividend Tracking
- Record dividend information
- Track dividend income
- Monitor upcoming dividend payments
- View dividend history

### 📈 Reports & Analytics
- Portfolio value history
- Asset allocation
- Transaction activity
- Top holdings
- Portfolio performance visualization

---

## 🛠️ Technology Stack

### Frontend
- HTML5
- CSS3
- JavaScript
- Bootstrap 5
- Bootstrap Icons
- Chart.js
- Razor Views *(planned)*

### Backend
- ASP.NET Core MVC
- C#
- Entity Framework Core
- ASP.NET Core Identity

### Database
- Oracle Database
- MySQL

### Development Tools
- Visual Studio Code
- Git
- GitHub

---

## 🗄️ Database Design

ShareSync uses a relational database designed around the core entities of a portfolio management system.

### Main Tables

| Table | Purpose |
|---|---|
| `APP_USERS` | Stores application users |
| `SECTORS` | Stores company sectors |
| `COMPANIES` | Stores listed companies |
| `PORTFOLIOS` | Stores user portfolios |
| `WATCHLISTS` | Stores user watchlists |
| `WATCHLIST_ITEMS` | Connects watchlists with companies |
| `TRANSACTIONS` | Stores BUY and SELL transactions |
| `DIVIDENDS` | Stores dividend information |
| `TRANSACTION_AUDIT` | Stores transaction audit records |
| `PORTFOLIO_SNAPSHOTS` | Stores historical portfolio values |

### Main Relationships

```text
APP_USERS
   │
   ├── PORTFOLIOS
   │       │
   │       ├── TRANSACTIONS
   │       │       │
   │       │       └── COMPANIES
   │       │
   │       └── PORTFOLIO_SNAPSHOTS
   │
   └── WATCHLISTS
           │
           └── WATCHLIST_ITEMS
                   │
                   └── COMPANIES
                           │
                           └── DIVIDENDS