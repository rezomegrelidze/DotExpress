using System.Text;

var app = new DotExpress.Server.HttpServer("http://localhost:5000/");

var todos = new List<dynamic>();
var id = 1;

// Example middleware: log every request
app.Use((req, res, next) => {
    Console.WriteLine($"[{DateTime.Now}] {req.method} {req.url}");
    next();
});

// Example middleware: add a custom header
app.Use((req, res, next) => {
    res.setHeader("X-Powered-By", "DotExpress");
    next();
});

app.UseJson(); // Middleware to parse JSON bodies

app.UseStatic("wwwroot"); // Serve static files from wwwroot

// GET /todos - returns all todos
app.Get("/todos", (req, res) => {
    res.status(200).json(todos);
});

// GET /hello - send plain text
app.Get("/hello", (req, res) => {
    res.send("Hello, world!");
});

// GET /redirect - redirect to /todos
app.Get("/redirect", (req, res) => {
    res.redirect("/todos");
});

// POST /todos - add a todo
app.Post("/todos", (req, res) => {
    var todo = new { id = id++, text = req.body.text };
    todos.Add(todo);
    res.status(201).json(todo);
});

// PUT /todos/:id - update a todo
app.Put("/todos/:id", (req, res) => {
    var todoId = int.Parse(req.parameters.id);
    var todo = todos.FirstOrDefault(t => t.id == todoId);
    if (todo != null)
    {
        todo.text = req.body.text;
        res.json(todo);
    }
    else
    {
        res.status(404).send("Not found");
    }
});

// DELETE /todos/:id - delete a todo
app.Delete("/todos/:id", (req, res) => {
    var todoId = int.Parse(req.parameters.id);
    todos.RemoveAll(t => t.id == todoId);
    res.sendStatus(204);
});

// GET /query - demonstrate query string usage
app.Get("/query", (req, res) => {
    res.json(req.query);
});

// GET /headers - demonstrate custom headers
app.Get("/headers", (req, res) => {
    res.setHeader("X-Test", "HeaderValue");
    res.send("Headers set!");
});

app.Start();

Console.WriteLine("Server running at http://localhost:5000/");
Console.WriteLine("Press Enter to stop...");
Console.ReadLine();