require('babel-register')
const React = require('react')
const ReactDOMServer = require('react-dom/server')
require('sharp-pad-dump-react')
const dump = require('sharp-pad-dump')
const { Action, Form, listen, clearHandlers, events, setPort } = require('sharp-pad-forms')
const getPort = require('get-port')
global.React = React
global.Component = React.Component
global.Fragment = React.Fragment
global.dump = dump
global.Action = Action
global.Form = Form
global._clearHandlers = clearHandlers
global.$ = '$'
dump.sourcemaps = false
dump.wrapCallSite = require('babel-register/node_modules/source-map-support').wrapCallSite
dump.source = function (source, value, accessor) {
  if (!source || source.length > 30) {
    return null
  }

  if (value && value.$type === 'html') {
    return null
  }

  const index = source.indexOf('[$]')
  if (index !== -1) {
    return source.substring(0, index).trim()
  }

  return source
}

dump.hook('$', true)
let httpPort
const entry = './src/' + (process.argv[2] || 'main')
getPort()
  .then(port => {
    setPort(port)
    httpPort = port
    return dump.clear()
  })
  .then(() => {
    if (dump.console) {
      throw new Error()
    }

    require(entry)
  })
  .catch((e) => {
    dump.console = {
      data(item, title) {
        if (title) {
          console.log(title)
        }

        if (React.isValidElement(item)) {
          console.log('<<Install SharpPad extension to display html>>')
        } else {
          console.log(item)
        }
      },
      html() {
        console.log('<<Install SharpPad extension to display html>>')
      }
    }

    require(entry)
  })

events.once('newElement', () => {
  if (dump.console) {
    return
  }

  listen(httpPort)
})
