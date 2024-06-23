import { useEffect } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";

function SupportLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (
      !location.pathname.includes("faq") &&
      !location.pathname.includes("notice")
    ) {
      navigate("faq");
    }
  }, [location.pathname]);

  return <Outlet />;
}

export default SupportLayout;
