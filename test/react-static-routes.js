

import React, { Component } from 'react'
import { Route } from 'react-router-dom'
import universal, { setHasBabelPlugin } from 'react-universal-component'
import { cleanPath } from 'react-static'



setHasBabelPlugin()

const universalOptions = {
  loading: () => null,
  error: () => {
    console.error(props.error);
    return <div>An unknown error has occured loading this page. Please reload your browser and try again.</div>;
  },
}

  const t_0 = universal(import('../src/components/content'), universalOptions)
const t_1 = universal(import('../src/components/404'), universalOptions)
    

// Template Map
const componentsByTemplateID = [
  t_0,
t_1
]

// Template Tree
const templateIDsByPath = {
  '404': 1
}

// Get template for given path
const getComponentForPath = path => {
  return componentsByTemplateID[templateIDsByPath[path]]
}

global.reactStaticGetComponentForPath = getComponentForPath
global.reactStaticRegisterTemplateIDForPath = (path, id) => {
  templateIDsByPath[path] = id
}

export default class Routes extends Component {
  render () {
    const { component: Comp, render, children } = this.props
    const renderProps = {
      componentsByTemplateID,
      templateIDsByPath,
      getComponentForPath
    }

    if (Comp) {
      return (
        <Comp
          {...renderProps}
        />
      )
    }

    if (render || children) {
      return (render || children)(renderProps)
    }

    // This is the default auto-routing renderer
    return (
      <Route path='*' render={props => {
        let Comp = getComponentForPath(cleanPath(props.location.pathname))
        if (!Comp) {
          Comp = getComponentForPath('404')
        }
        return Comp ? <Comp {...props} /> : null
      }} />
    )
  }
}

    