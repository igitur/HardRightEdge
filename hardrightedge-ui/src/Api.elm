module Api exposing (..)

import Domain exposing (..)
import Json.Decode as JsonD
import Json.Encode as JsonE
import Http

baseUrl : String
baseUrl = "http://localhost:8080"

platformDecoder : JsonD.Decoder (Maybe Platform)
platformDecoder =
  JsonD.int
  |> JsonD.andThen (\id -> JsonD.succeed (platform id))

securityPlatformDecoder: JsonD.Decoder SecurityPlatform
securityPlatformDecoder =
  JsonD.map3 SecurityPlatform
    (JsonD.maybe <| JsonD.field "shareId"  JsonD.int)
    (JsonD.field "platform" platformDecoder)
    (JsonD.field "symbol" JsonD.string)

securityPlatformsDecoder : JsonD.Decoder (List SecurityPlatform)
securityPlatformsDecoder = JsonD.list securityPlatformDecoder

securitiesDecoder : JsonD.Decoder (List Security)
securitiesDecoder = JsonD.list securityDecoder

securityDecoder : JsonD.Decoder Security
securityDecoder =
  JsonD.map3 Security
    (JsonD.maybe <| JsonD.field "id"  JsonD.int)
    (JsonD.field "name"  JsonD.string)
    (JsonD.field "platforms" <| securityPlatformsDecoder)

encodeSecurityPlatform : SecurityPlatform -> JsonE.Value
encodeSecurityPlatform securityPlatform =
  JsonE.encode 0 <|
    JsonE.object
      [ ("securityId", (case securityPlatform.securityId of
                        Nothing -> JsonE.null
                        Just secId -> JsonE.int secId))]

encodeSecurity : Security -> JsonE.Value
encodeSecurity security =
  JsonE.encode 0 <|
    JsonE.object 
      [ ("id",  (case security.id of
                  Nothing -> JsonE.null
                  Just id -> JsonE.int id))
        ("name", JsonE.string security.name),
        ("previousName", JsonE.null),
        ("prices", JsonE.list []),
        ("platforms", JsonE.list []), -- TODO
        ("currency", JsonE.null) ]

getPortfolio : (Result Http.Error (List Security) -> msg) -> Cmd msg
getPortfolio msg =
  Http.get (baseUrl ++ "/portfolio") securitiesDecoder
  |> Http.send msg

updateSecurity : Security -> (Result Http.Error Security -> msg) -> Cmd msg
updateSecurity security msg =
  Http.request
    { method  = "PUT",
      headers = [],
      url     = baseUrl ++ "/portfolio/" ++ toString security.id,
      body    = Http.stringBody "application/json" <| 
    }
