import { convertPath } from "@/_utils/image";
import { useState } from "react";

interface FallbackImageProps {
  src: string;
}

const FallbackImage = ({ src }: FallbackImageProps) => {
  const [url, setUrl] = useState(src);

  const handleError = () => {
    if (url.startsWith("../")) {
      setUrl(convertPath(url));
    }
  };

  return <img src={url} onError={handleError} />;
};

export default FallbackImage;
