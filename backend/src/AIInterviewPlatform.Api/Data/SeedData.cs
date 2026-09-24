using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Data;

/// <summary>
/// Built-in question banks covering the JD's domains (DSA, OOP, Logic, DB, Web, AI/ML, Communication).
/// </summary>
public static class SeedData
{
    public static List<QuestionBank> Build()
    {
        var sde = new QuestionBank
        {
            Id = IdGen.New(),
            Name = "SDE Campus — Full Stack (.NET/React)",
            Description = "Mixed-difficulty coding & concept bank modeled on campus SDE interviews: DSA, OOP, Databases, Web, and communication."
        };
        var ai = new QuestionBank
        {
            Id = IdGen.New(),
            Name = "AI/ML & Python Practice",
            Description = "Practice bank for jobs touching Machine Learning, LLMs, and Python — with DSA and logic sprinkled in."
        };

        var q = new List<Question>();

        // ---------------- DSA ----------------
        q.Add(NewQ(Tags.Dsa, "Easy",
            "How does a hash table achieve O(1) average lookup? Explain collisions and how they are resolved.",
            new[] { "hash", "collision", "bucket" },
            new[] { "A hash function maps keys to array indices", "Collisions happen when two keys hash to the same index", "Chaining or open addressing resolve collisions", "Amortized O(1) with a good hash function and load factor" }));
        q.Add(NewQ(Tags.Dsa, "Easy",
            "Which data structure processes elements in First-In-First-Out (FIFO) order?",
            new[] { "queue" }, new[] { "Queue (FIFO), Stack is LIFO" }).Mcq(new[] { "Stack", "Queue", "Tree", "Graph" }, 1, 60));
        q.Add(NewQ(Tags.Dsa, "Medium",
            "Given an array of alphanumeric characters, how would you efficiently check if all elements are unique? State the time and space complexity.",
            new[] { "hashset", "set", "o(n)", "space" },
            new[] { "Insert into a hash set while scanning", "If an insert collides with existing value → duplicate found", "Time O(n), space O(n)", "Can trade space for time vs O(n²) nested loop" },
            "A set lets you test membership in O(1)."));
        q.Add(NewQ(Tags.Dsa, "Medium",
            "What is the time complexity of binary search on a sorted array of n elements?",
            new[] { "log" }, new[] { "O(log n)" }).Mcq(new[] { "O(n)", "O(log n)", "O(n log n)", "O(1)" }, 1, 60));
        q.Add(NewQ(Tags.Dsa, "Hard",
            "An array of size n-1 holds unique numbers from 1 to n with exactly one missing. Find the missing number — and explain the XOR-based approach.",
            new[] { "xor", "sum", "o(n)" },
            new[] { "XOR the full range 1..n with the array — duplicates cancel out", "Or use (n(n+1)/2) minus array sum", "Both are O(n) time", "XOR avoids overflow that the sum formula can hit" },
            "XOR of a number with itself is 0; XOR with 0 is the number itself."));
        q.Add(NewQ(Tags.Dsa, "Hard",
            "What is a Binary Search Tree? Describe how it could be used in a real product you use every day.",
            new[] { "left", "right", "sorted", "search" },
            new[] { "BST: each node has at most two children, left < node < right", "Search is O(log n) on a balanced tree", "Used in autocomplete, spell-check, autocomplete search indexes, dictionaries", "Balancing keeps operations fast" }));

        // ---------------- OOP ----------------
        q.Add(NewQ(Tags.Oop, "Easy",
            "Explain the four pillars of OOP with one real-world example each.",
            new[] { "encapsulation", "inheritance", "polymorphism", "abstraction" },
            new[] { "Encapsulation: bundle data + methods, hide internals (e.g., a BankAccount class)", "Inheritance: a SavingsAccount extends Account", "Polymorphism: Draw() behaves differently for Circle vs Square", "Abstraction: an interface like IPayment hides the implementation" }));
        q.Add(NewQ(Tags.Oop, "Easy",
            "Which OOP pillar keeps an object's internal state private and only reachable through methods?",
            new[] { "encapsulation" }, new[] { "Encapsulation — access modifiers (private/protected)" }).Mcq(
            new[] { "Abstraction", "Encapsulation", "Inheritance", "Polymorphism" }, 1, 60));
        q.Add(NewQ(Tags.Oop, "Medium",
            "Explain the difference between method overloading and method overriding. Can a static method be overridden? Why?",
            new[] { "compile", "runtime", "static", "signature" },
            new[] { "Overloading: same name, different parameters — resolved at compile time", "Overriding: derived class re-implements a virtual method — resolved at runtime", "Static methods belong to the type, not the instance → they cannot be overridden, only hidden", "Use 'virtual/override' in C#" }));
        q.Add(NewQ(Tags.Oop, "Medium",
            "In C#, can a static method be overridden in a derived class?",
            new[] { "no" }, new[] { "No — static methods are class-level; hiding is possible but not overriding" }).Mcq(
            new[] { "Yes", "No", "Only in sealed classes", "Only with the virtual keyword" }, 1, 60));
        q.Add(NewQ(Tags.Oop, "Hard",
            "A base class has a static method and the derived class declares a method with the same signature. What happens when you call it via the derived class?",
            new[] { "hiding", "new", "shadow" },
            new[] { "The derived method hides the base static method", "C# requires the 'new' keyword to make intent explicit (warning otherwise)", "It is not polymorphism — dispatch is by the compile-time type", "Static members are resolved against the declaring type" }));

        // ---------------- Logic ----------------
        q.Add(NewQ(Tags.Logic, "Easy",
            "You are outside a room with three switches; one switch controls a bulb inside the room. You may enter the room once. How do you find which switch controls the bulb?",
            new[] { "heat", "warm", "on", "off" },
            new[] { "Turn switch A on for a few minutes, then off", "Turn switch B on and leave it on, enter the room", "Bulb is: ON → B; OFF but warm → A; OFF and cold → C" }));
        q.Add(NewQ(Tags.Logic, "Easy",
            "A clock ticks 6 times; the time between the first and last tick is 30 seconds. How long does it take to tick 12 times?",
            new[] { "gap", "interval" }, new[] { "6 ticks = 5 gaps → 30/5 = 6s per gap", "12 ticks = 11 gaps → 66 seconds" }).Mcq(
            new[] { "55 seconds", "60 seconds", "66 seconds", "72 seconds" }, 2, 90));
        q.Add(NewQ(Tags.Logic, "Medium",
            "How would you efficiently check whether a positive integer n is a power of three? Discuss time and space complexity.",
            new[] { "modulo", "division", "loop", "log" },
            new[] { "Loop: keep dividing by 3 while divisible", "n % 3 must be 0 at each step until n==1", "Or check 3^19 == largest 32-bit power → n divides it evenly", "Time O(log₃ n), space O(1)" }));
        q.Add(NewQ(Tags.Logic, "Medium",
            "What number comes next in the sequence? 2, 3, 5, 8, ...",
            new[] { "fibonacci" }, new[] { "Fibonacci: 5+3=8, next is 8+5=13" }).Mcq(
            new[] { "10", "11", "12", "13" }, 3, 60));
        q.Add(NewQ(Tags.Logic, "Hard",
            "Explain the pigeonhole principle to a friend who has never studied math, using a real-world example.",
            new[] { "n+1", "same", "at least", "container" },
            new[] { "If you put 6 pigeons into 5 holes, at least one hole holds 2 pigeons", "If more items than containers, some container holds ≥ 2", "Used to prove repetition exists (e.g., birthdays, checksums)", "A foundation for many proofs in CS" }).Voice());

        // ---------------- DB ----------------
        q.Add(NewQ(Tags.Db, "Easy",
            "What is the difference between SQL and NoSQL databases, and when would you choose each?",
            new[] { "schema", "mongodb", "document", "relational", "scale" },
            new[] { "SQL: relational, fixed schema, ACID, great for joins (MySQL, PostgreSQL)", "NoSQL: flexible schema, horizontal scale, document/key-value (MongoDB, Redis)", "Choose SQL for transactional, relational data; NoSQL for speed, scale and evolving models", "Many products use both (polyglot persistence)" }));
        q.Add(NewQ(Tags.Db, "Easy",
            "Which of the following is a NoSQL key-value store?",
            new[] { "redis" }, new[] { "Redis stores data as key–value pairs in memory" }).Mcq(
            new[] { "MySQL", "Redis", "PostgreSQL", "Oracle DB" }, 1, 60));
        q.Add(NewQ(Tags.Db, "Medium",
            "Explain why a database index makes queries faster, and describe its trade-offs.",
            new[] { "faster", "lookup", "write", "b-tree" },
            new[] { "An index is like a book's index — an ordered structure (B-tree) for O(log n) lookups", "Avoids full table scans", "Trade-off: extra storage + slower writes/updates", "Choose columns used in WHERE/JOIN" }));
        q.Add(NewQ(Tags.Db, "Medium",
            "In the CAP theorem, what do the letters C, A, and P stand for?",
            new[] { "consistency", "availability", "partition" }, new[] { "Consistency, Availability, Partition tolerance", "A distributed system can guarantee at most two strongly" }).Mcq(
            new[] { "Consistency, Availability, Partition tolerance", "Create, Availability, Privacy", "Consistency, Atomic, Partition", "Cache, Availability, Privacy" }, 0, 60));

        // ---------------- Web ----------------
        q.Add(NewQ(Tags.Web, "Easy",
            "Walk me through what happens when you type a URL and press Enter.",
            new[] { "dns", "tcp", "http", "request", "server" },
            new[] { "Browser checks cache → resolves the domain via DNS", "Opens a TCP connection (HTTPS: TLS handshake)", "Sends an HTTP request; server processes and returns the response", "Browser parses HTML/CSS/JS and renders the page" }));
        q.Add(NewQ(Tags.Web, "Easy",
            "Which HTTP method is meant to delete a resource on the server?",
            new[] { "delete" }, new[] { "DELETE — idempotent by convention" }).Mcq(
            new[] { "GET", "POST", "PUT", "DELETE" }, 3, 60));
        q.Add(NewQ(Tags.Web, "Medium",
            "Explain how JWT (JSON Web Token) authentication works end-to-end in a web application.",
            new[] { "header", "payload", "signature", "stateless", "token" },
            new[] { "On login the server issues a signed token (header.payload.signature)", "Client stores it (localStorage/HttpOnly cookie) and sends it in the Authorization header", "Server verifies signature with its secret; no server-side session needed", "Benefits: stateless, horizontal scaling; caveat: token revocation is hard" }));
        q.Add(NewQ(Tags.Web, "Medium",
            "Which React hook is used to manage local component state?",
            new[] { "usestate" }, new[] { "useState returns [state, setter]" }).Mcq(
            new[] { "useEffect", "useState", "useRef", "useMemo" }, 1, 60));
        q.Add(NewQ(Tags.Web, "Hard",
            "Why is Redis so fast, and where would you add Redis caching in a full-stack web app?",
            new[] { "memory", "in-memory", "cache", "latency", "eviction" },
            new[] { "Redis is in-memory — microsecond reads, no disk I/O", "Single-threaded event loop avoids locking overhead", "Cache hot data: question banks, user profiles, session tokens, rate counters", "Use TTL + LRU eviction; invalidate on writes (cache-aside pattern)" }));

        // ---------------- AI/ML ----------------
        q.Add(NewQ(Tags.Ml, "Easy",
            "In simple terms, what is a Large Language Model (LLM)?",
            new[] { "transformer", "text", "predict", "tokens", "trained" },
            new[] { "LLMs are neural networks trained on huge text corpora", "They predict the next token given context", "Built on the transformer architecture (attention)", "They power chatbots, summarization, code generation" }));
        q.Add(NewQ(Tags.Ml, "Easy",
            "YOLO (You Only Look Once) is best known for which task in computer vision?",
            new[] { "detection", "bounding" }, new[] { "Real-time object detection with bounding boxes in one pass" }).Mcq(
            new[] { "Text generation", "Object detection", "Speech synthesis", "Image generation" }, 1, 60));
        q.Add(NewQ(Tags.Ml, "Medium",
            "What is the difference between image classification and object detection?",
            new[] { "class", "box", "bounding", "location" },
            new[] { "Classification: one label for the whole image", "Detection: multiple objects with bounding boxes + class per box", "Detection includes localization (region proposals or single-shot like YOLO)", "Metrics differ: accuracy vs mAP (mean average precision)" }));
        q.Add(NewQ(Tags.Ml, "Medium",
            "What does NER stand for in NLP, and give one real-world use case?",
            new[] { "named", "entity", "recognition" },
            new[] { "Named Entity Recognition", "Extracts entities like names, dates, places from text", "Used in search, chatbots, resume parsing, document automation" }).Mcq(
            new[] { "Natural Entity Regression", "Named Entity Recognition", "Neural Encoding Routine", "None of the above" }, 1, 60));
        q.Add(NewQ(Tags.Ml, "Hard",
            "You mention YOLO and LLMs in your profile. How would you combine object detection with an LLM to build a useful product?",
            new[] { "detect", "yolo", "llm", "context" },
            new[] { "YOLO detects objects in frames (e.g., proctoring video, shelf monitoring)", "LLM interprets the detections into sentences/decisions", "Example: shopping-shoplifting alerts or exam-proctoring summaries", "Shows systems thinking — multiple models orchestrating" }).Voice());

        // ---------------- Communication ----------------
        q.Add(NewQ(Tags.Comm, "Easy",
            "Introduce yourself in under 60 seconds. Cover your technical strengths, projects, and what you want next.",
            new[] { "react", "python", "mongodb", "project", "learn" },
            new[] { "Name, college, branch, batch", "One flagship full-stack project + one AI project", "Stack alignment: React, Python, MongoDB/Redis", "Close: open to Bangalore/Remote, ready to grow" }).Voice(90));
        q.Add(NewQ(Tags.Comm, "Medium",
            "Tell me about a time you solved a difficult technical problem under pressure. Use the Situation-Action-Result format.",
            new[] { "situation", "action", "result", "bug" },
            new[] { "Situation: a bug/tight deadline on a project", "Action: broke the problem down, debugged systematically, used docs/resources", "Result: shipped working feature + what you learned", "Keep it to ~90 seconds, honest and specific" }).Voice());
        q.Add(NewQ(Tags.Comm, "Hard",
            "Why do you want to work at TESCRA/ACHNET specifically, and what will you bring in the first 90 days?",
            new[] { "stack", "mongodb", "react", "ai", "learn", "growth" },
            new[] { "Reference the product (AI-driven talent platform) — why it excites you", "Your stack matches: C#/.NET, React, MongoDB, Redis, Python", "Mention willingness to learn quickly in an agile team", "Close with enthusiasm and a concrete contribution you'll make" }).Voice());

        foreach (var item in q)
        {
            // SDE bank: DSA, OOP, Web, Communication. AI bank: ML, Logic, DB.
            if (item.Tag is Tags.Dsa or Tags.Oop or Tags.Web or Tags.Comm)
                sde.Questions.Add(item);
            else
                ai.Questions.Add(item);
        }

        // Guarantee both banks have some questions.
        if (ai.Questions.Count == 0)
            ai.Questions.AddRange(sde.Questions.Where(x => x.Tag == Tags.Logic || x.Tag == Tags.Db).Take(3));

        return new List<QuestionBank> { sde, ai };
    }

    private static Question NewQ(string tag, string difficulty, string text, string[] keywords, string[] points, string? hint = null)
    {
        var id = IdGen.New();
        return new Question
        {
            Id = id,
            Text = text,
            Tag = tag,
            Difficulty = difficulty,
            Type = QuestionType.Subjective,
            ExpectedKeywords = keywords.ToList(),
            ModelPoints = points.ToList(),
            Hint = hint,
            TimeLimitSeconds = difficulty switch { "Easy" => 120, "Medium" => 180, _ => 300 }
        };
    }

    private static Question Mcq(this Question q, string[] options, int correct, int seconds)
    {
        q.Type = QuestionType.Mcq;
        q.Options = options.Select(o => new McqOption { Text = o }).ToList();
        q.CorrectIndex = correct;
        q.TimeLimitSeconds = seconds;
        return q;
    }

    private static Question Voice(this Question q, int? seconds = null)
    {
        q.Type = QuestionType.Voice;
        if (seconds.HasValue) q.TimeLimitSeconds = seconds.Value;
        return q;
    }
}