namespace Components

module SideMenu =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes

    type Model =
        { Open : bool }

    type Msg = ToggleMenu

    let init() = { Open = false }

    let update msg model =
        match msg with
        | ToggleMenu -> { model with Open = not model.Open }

    let styles (theme : ITheme) : IStyles list = []

    let private view' (classes : IClasses) (model : Model) dispatch =
        nav []
            [ drawer [ DrawerProp.Variant DrawerVariant.Temporary
                       MaterialProp.OnClose(fun _ -> ToggleMenu |> dispatch)
                       MaterialProp.Open model.Open ]
                  [ toolbar []
                        [ typography
                              [ TypographyProp.Variant TypographyVariant.H6 ]
                              [ str "Menu" ] ] ] ]

    // Boilerplate code
    // Workaround for using JSS with Elmish
    // https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract model : Model with get, set
        abstract dispatch : (Msg -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.model p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() =
            ReactElementType.create !!viewWithStyles this.props []

    let view (model : Model) (dispatch : Msg -> unit) : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.model <- model
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
