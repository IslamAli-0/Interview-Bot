# System Architecture: Telegram Interview Master Bot

## Project Vision
A hybrid full-stack application operating simultaneously as a personal Telegram Subscription Bot and a local Web API. Its primary purpose is to serve as an automated, daily technical interviewer for developers mastering backend engineering, with a sharp focus on the .NET Core ecosystem and C# fundamentals.

The bot broadcasts daily challenges directly to subscribers' private DMs, evaluates their text replies individually as a strict senior engineer, and provides immediate grading along with a pre-generated optimal model solution. Aggregate statistics, leaderboards, and system difficulty configurations are isolated on a web dashboard to keep the Telegram interaction focused on engineering.

## Core Technology Stack
- Backend framework: C# / ASP.NET Core (.NET 8), running as a unified host (WebApplication)
- Bot library: Telegram.Bot (NuGet), using ASP.NET Core controllers to receive Telegram webhooks
- AI integration: Google GenAI SDK
- Primary engine: gemini-2.5-flash (challenge generation and answer grading)
- Embedding model: text-embedding-004 (semantic question deduplication)
- Database: SQLite (local single-file DB InterviewBot.db)
- Frontend dashboard: static HTML/CSS/JS served from wwwroot
- Deployment target: Linux-x64, listens explicitly on port 8080

## Database Schema (SQLite)

### Users
| Column | Type | Notes |
| --- | --- | --- |
| TelegramId | INTEGER | Primary key, Telegram private chat ID |
| Name | TEXT | User display name |
| Score | INTEGER | Cumulative points |
| IsSubscribed | INTEGER | 0/1, default 1 |

### AskedQuestions
| Column | Type | Notes |
| --- | --- | --- |
| Id | INTEGER | Primary key, autoincrement |
| QuestionText | TEXT | AI-generated technical question |
| ModelSolution | TEXT | Pre-generated optimal answer |
| VectorEmbedding | BLOB | Embedding vector for deduplication |
| AskedOn | DATETIME | Broadcast timestamp |

### AnswerHistory
| Column | Type | Notes |
| --- | --- | --- |
| Id | INTEGER | Primary key, autoincrement |
| TelegramId | INTEGER | Foreign key to Users |
| QuestionId | INTEGER | Foreign key to AskedQuestions |
| GivenAnswer | TEXT | Raw response text |
| ScoreReceived | INTEGER | Score from 0 to 10 |

### Settings
| Column | Type | Notes |
| --- | --- | --- |
| Key | TEXT | Primary key, setting identifier |
| Value | TEXT | Active configuration value |

## Core Game Loop and AI Logic

### 1. Daily Challenge Generation and Semantic Deduplication
A BackgroundService triggers once every 24 hours at midnight Egypt Standard Time:
- Difficulty scaling: base difficulty climbs from level 4 to level 10 over a 7-day cycle (totalDays % 7), unless an administrative freeze is active
- Flashback review: 20 percent chance of selecting a lower level
- Deduplication: AI drafts a question, generates a vector embedding, and calculates cosine similarity against the last 30 stored embeddings. If similarity > 0.85, the draft is rejected and regenerated
- Simultaneous solution generation: once accepted, the AI generates a model solution (under 1800 characters) with explanations and idiomatic C# examples
- Persistence: question and solution are stored in a single transaction

### 2. Broadcast
The system queries all users where IsSubscribed = 1 and sends QuestionText to their private DMs asynchronously.

### 3. Immediate Grading and Feedback Loop
When a subscriber replies with a non-command text message:
- Validate the user has not already submitted an answer for the active question
- gemini-2.5-flash evaluates as a strict senior .NET engineer
- AI returns a strict format: "SCORE: X out of 10" and a brief critique
- Backend parses the score, updates total points, saves history, and replies with evaluation plus the cached model solution in a single Markdown message

## Web Dashboard and API Architecture
A lightweight CORS-enabled admin API is hosted alongside the Telegram webhook receiver. The dashboard is the exclusive location for viewing system metrics and gamification scores.

Endpoints:
- GET /api/dashboard/stats: leaderboard ordered by score and active difficulty configuration
- POST /api/settings/level/{newLevel}: freeze progression and lock difficulty
- DELETE /api/settings/level: clear override and resume automation

Static files: index.html and assets served from wwwroot.

## Command Reference (Telegram Standard Syntax)

Subscription management:
- /start or /subscribe: register user and enable subscription
- /unsubscribe: disable subscription without clearing leaderboard history

Training extensions:
- /ask or /test: generate an isolated question for the requesting user only
- /hint: return a conceptual hint for the active question

System utilities:
- /profile: display user stats (total score, challenges attempted)
- /help: list available commands
- /ping: validate network health and gateway latency
- /stats: show system metrics (uptime, subscriber headcount)
