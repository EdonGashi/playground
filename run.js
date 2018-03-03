require('babel-register')
const { wrapCallSite } = require('babel-register/node_modules/source-map-support')
const React = require('react')
const ReactDOMServer = require('react-dom/server')
require('sharp-pad-dump-react')
const dump = require('sharp-pad-dump')
const { Action, Form, listen, clearHandlers, events, setPort } = require('sharp-pad-forms')
const getPort = require('get-port')
const getCallsites = require('error-callsites')
const { dirname } = require('path')
global.React = React
global.Component = React.Component
global.Fragment = React.Fragment
global.dump = dump
global.Action = Action
global.Form = Form
global._clearHandlers = clearHandlers
global.$ = dump
dump.sourcemaps = false
dump.wrapCallSite = wrapCallSite
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
function run() {
  const redirect = require(entry)
  if (redirect && typeof redirect === 'string') {
    require('./src/' + redirect)
  }
}

dump.before = function (data, title, accessor, trace) {
  if (accessor !== '$') {
    return true
  }

  const msg = 'Cannot evaluate.'
  if (!trace) {
    throw new Error(msg)
  }

  let callsite = getCallsites(trace)[1]
  if (!callsite) {
    throw new Error(msg)
  }

  callsite = wrapCallSite(callsite)
  console.log(dirname(callsite.getFileName()))
  if (!dirname(callsite.getFileName()).includes('src')) {
    throw new Error(msg)
  }
}

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

    run()
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

    run()
  })

events.once('newElement', () => {
  if (dump.console) {
    return
  }

  listen(httpPort)
})
