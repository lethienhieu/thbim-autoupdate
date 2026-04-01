const http = require('http');
const fs = require('fs');
const path = require('path');
http.createServer((req, res) => {
  const url = req.url === '/' ? '/mockup.html' : req.url;
  const file = path.join(__dirname, url);
  if (fs.existsSync(file)) {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    fs.createReadStream(file).pipe(res);
  } else {
    res.writeHead(404);
    res.end('Not found');
  }
}).listen(3001, () => console.log('Serving on http://localhost:3001'));
