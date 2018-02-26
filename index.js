require('babel-register')
const React = require('react')
const ReactDOMServer = require('react-dom/server')
require('sharp-pad-dump-react')
const dump = require('sharp-pad-dump')
const { Action, Form, listen, clearHandlers, events } = require('sharp-pad-forms')
const getPort = require('get-port')
global.React = React
global.Component = React.Component
global.dump = dump
global.Action = Action
global.Form = Form
global._clearHandlers = clearHandlers
events.once('newElement', () => {
  if (dump.console) {
    return
  }

  getPort()
    .then(port => listen(port))
    .catch(err => console.error(err))
})

dump.clear()
  .then(() => {
    require('./src/main.js')
  })
  .catch((e) => {
    dump.console = {
      data(item, title) {
        if (title) {
          console.log(title)
        }

        if (React.isValidElement(item)) {
          console.log(ReactDOMServer.renderToStaticMarkup(item))
        } else {
          console.log(item)
        }
      }
    }

    require('./src/main.js')
  })
