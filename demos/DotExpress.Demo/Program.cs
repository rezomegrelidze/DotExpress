using System.Text;

var app = new DotExpress.Server.HttpServer("http://localhost:5000/");

var todos = new List<dynamic>();
var id = 1;

app.UseJson(); // hypothetical middleware to parse JSON bodies

app.Get("/todos", (req, res) => {
    res.json(todos);
});

app.Post("/todos", (req, res) => {
    var todo = new { id = id++, text = req.body.text };
    todos.Add(todo);
    res.json(todo);
});

app.Delete("/todos/:id", (req, res) => {
    var todoId = int.Parse(req.parameters.id);
    todos.RemoveAll(t => t.id == todoId);
    res.sendStatus(204);
});

app.Start();

Console.ReadLine();